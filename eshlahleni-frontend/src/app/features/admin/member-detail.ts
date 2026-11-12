import { DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { FormBuilder, FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, catchError, combineLatest, finalize, of, startWith, switchMap } from 'rxjs';
import { ApiIssue, apiIssue, applyFieldErrors } from '../../core/api-errors';
import { Member, MemberStatus, memberStatuses } from '../../core/api.models';
import { AdminMembersService } from '../../core/membership.services';
import { ApiError, CalendarDatePipe, ConfirmDialog, Field, LoadingState, StatusBadge } from '../../shared/ui';
import { canonicalPhone, emailAddress, trimmedName } from '../../shared/validation';

@Component({
  imports: [DatePipe, ReactiveFormsModule, RouterLink, ApiError, CalendarDatePipe, ConfirmDialog, Field, LoadingState, StatusBadge],
  template: `<nav class="breadcrumb" aria-label="Breadcrumb"><a routerLink="/admin/members">Members</a><span aria-hidden="true">/</span><span>Member details</span></nav>
    <app-api-error [issue]="issue()" [canRetry]="!saving()" (retry)="reload()" />
    @if (notice()) { <div class="alert alert--success" role="status">{{ notice() }}</div> }
    @if (loading()) { <app-loading text="Loading member details…" /> }
    @if (!loading() && member(); as person) {
      <div class="admin-page-heading detail-heading"><div><span class="section-tag">MEMBERSHIP / {{ person.memberNumber }}</span><h1>{{ person.firstName }}<br />{{ person.lastName }}</h1><p>Member since {{ person.joinedAtUtc | date:'d MMMM yyyy':'+0200' }}</p></div><app-status [status]="person.status" /></div>
      <div class="detail-layout"><section class="panel"><div class="panel-heading profile-heading"><div><span class="eyebrow">THE MEMBER</span><h2>PROFILE DETAILS.</h2></div>@if (!editing()) { <button class="text-button" type="button" (click)="edit()" [disabled]="saving()">Edit profile ↗</button> }</div>
        @if (editing()) { <form [formGroup]="form" (ngSubmit)="saveProfile()" novalidate [attr.aria-busy]="saving()"><div class="form-grid"><app-field id="memberFirstName" label="First name" [control]="form.controls.firstName"><input id="memberFirstName" formControlName="firstName" required autocomplete="given-name" aria-describedby="memberFirstName-error" [attr.aria-invalid]="form.controls.firstName.touched && form.controls.firstName.invalid" /></app-field><app-field id="memberLastName" label="Surname" [control]="form.controls.lastName"><input id="memberLastName" formControlName="lastName" required autocomplete="family-name" aria-describedby="memberLastName-error" [attr.aria-invalid]="form.controls.lastName.touched && form.controls.lastName.invalid" /></app-field><app-field id="memberPhone" label="Phone number" [control]="form.controls.phoneNumber"><input id="memberPhone" formControlName="phoneNumber" type="tel" required autocomplete="tel" aria-describedby="memberPhone-error" [attr.aria-invalid]="form.controls.phoneNumber.touched && form.controls.phoneNumber.invalid" /></app-field><app-field id="memberEmail" label="Email address" [control]="form.controls.email"><input id="memberEmail" formControlName="email" type="email" required autocomplete="email" aria-describedby="memberEmail-error" [attr.aria-invalid]="form.controls.email.touched && form.controls.email.invalid" /></app-field></div><p class="profile-edit-note">Only names and contact details can be updated here. The original application and membership identity stay unchanged.</p><div class="button-group"><button class="button" type="submit" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Save profile' }}</button><button class="button button--outline" type="button" [disabled]="saving()" (click)="cancelEdit()">Cancel</button></div></form> }
        @else { <dl class="details-grid"><div><dt>First name</dt><dd>{{ person.firstName }}</dd></div><div><dt>Surname</dt><dd>{{ person.lastName }}</dd></div><div><dt>Date of birth</dt><dd>{{ person.dateOfBirth | calendarDate }}</dd></div><div><dt>Phone</dt><dd>{{ person.phoneNumber }}</dd></div><div class="full-width"><dt>Email address</dt><dd>{{ person.email }}</dd></div><div class="full-width"><dt>Joined</dt><dd>{{ person.joinedAtUtc | date:'d MMM yyyy, HH:mm':'+0200' }} SAST</dd></div></dl> }
        <div class="source-application"><span class="contact-label">ORIGINAL APPLICATION</span><a [routerLink]="['/admin/applications', person.applicationId]">View source application ↗</a></div>
      </section>
      <aside><div class="membership-card"><img src="assets/branding/eshlahleni-logo.jpeg" alt="Es’hlahleni Social Club emblem" width="80" height="89" /><span class="eyebrow">ES'HLAHLENI SOCIAL CLUB</span><span class="membership-card-label">MEMBER NUMBER</span><strong data-testid="member-number">{{ person.memberNumber }}</strong><span class="membership-card-name">{{ person.firstName }} {{ person.lastName }}</span><span class="membership-card-footer">EST. 2025 <span aria-hidden="true">✳</span> A PLACE TO BELONG</span></div>
        <section class="panel member-status-panel"><span class="eyebrow">MEMBERSHIP STATUS</span><h2>KEEP IT CURRENT.</h2><label for="detailMemberStatus">Set membership status</label><select id="detailMemberStatus" [formControl]="statusControl">@for (status of statuses; track status) { <option [value]="status">{{ status }}</option> }</select><button type="button" class="button button--full" [disabled]="saving() || editing() || statusControl.value === person.status" (click)="confirmStatus.set(true)">Update status ↗</button><p>Changing status does not change this member’s identity or application.</p></section>
      </aside></div>
    }
    @if (confirmStatus()) { <app-confirm-dialog [title]="'Set membership to ' + statusControl.value + '?'" [message]="'This changes the membership status for ' + (member()?.memberNumber ?? '') + '. The change will be recorded in the club’s administration history.'" confirmLabel="Confirm status change" [busy]="saving()" (cancel)="confirmStatus.set(false)" (confirm)="saveStatus()" /> }
  `,
})
export class MemberDetailPage {
  private readonly api = inject(AdminMembersService); private readonly route = inject(ActivatedRoute); private readonly destroy = inject(DestroyRef); private readonly fb = inject(FormBuilder).nonNullable;
  private readonly refresh = new Subject<void>(); private id = '';
  readonly member = signal<Member | null>(null); readonly loading = signal(true); readonly saving = signal(false); readonly editing = signal(false); readonly confirmStatus = signal(false);
  readonly issue = signal<ApiIssue | null>(null); readonly notice = signal(''); readonly statuses = memberStatuses;
  readonly statusControl = new FormControl<MemberStatus>('Active', { nonNullable: true });
  readonly form = this.fb.group({ firstName: ['', [Validators.required, trimmedName]], lastName: ['', [Validators.required, trimmedName]], phoneNumber: ['', [Validators.required, canonicalPhone]], email: ['', [Validators.required, emailAddress]] });
  constructor() {
    combineLatest([this.route.paramMap, this.refresh.pipe(startWith(undefined))]).pipe(switchMap(([params]) => {
      this.id = params.get('memberId') ?? ''; this.loading.set(true); this.issue.set(null); this.member.set(null); this.editing.set(false); this.confirmStatus.set(false); this.notice.set('');
      return this.api.get(this.id).pipe(catchError(error => { this.issue.set(apiIssue(error)); return of(null); }), finalize(() => this.loading.set(false)));
    }), takeUntilDestroyed(this.destroy)).subscribe(member => { this.member.set(member); if (member) { this.populate(member); this.statusControl.setValue(member.status); } });
  }
  private populate(member: Member): void { this.form.reset({ firstName: member.firstName, lastName: member.lastName, phoneNumber: member.phoneNumber, email: member.email }); }
  reload(): void { if (!this.saving()) this.refresh.next(); }
  edit(): void { const member = this.member(); if (member) { this.populate(member); this.editing.set(true); this.notice.set(''); } }
  cancelEdit(): void { this.editing.set(false); const member = this.member(); if (member) this.populate(member); }
  saveProfile(): void {
    if (this.saving()) return;
    this.form.markAllAsTouched(); if (this.form.invalid) return;
    this.saving.set(true); this.issue.set(null); this.notice.set(''); const id = this.id;
    this.api.update(id, this.form.getRawValue()).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.saving.set(false))).subscribe({
      next: member => { if (id !== this.id) return; this.member.set(member); this.populate(member); this.editing.set(false); this.statusControl.setValue(member.status); this.notice.set('Member profile updated.'); },
      error: error => { const issue = apiIssue(error, 'member'); this.issue.set(issue); applyFieldErrors(this.form, issue); },
    });
  }
  saveStatus(): void {
    if (this.saving() || this.editing() || !memberStatuses.includes(this.statusControl.value)) return;
    this.saving.set(true); this.issue.set(null); this.notice.set(''); const id = this.id;
    this.api.updateStatus(id, this.statusControl.value).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.saving.set(false))).subscribe({
      next: member => { if (id !== this.id) return; this.confirmStatus.set(false); this.member.set(member); this.statusControl.setValue(member.status); this.notice.set(`Membership status updated to ${member.status}.`); },
      error: error => { this.confirmStatus.set(false); this.issue.set(apiIssue(error, 'member')); },
    });
  }
}
