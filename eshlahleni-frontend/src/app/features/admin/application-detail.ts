import { DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, catchError, combineLatest, finalize, of, startWith, switchMap } from 'rxjs';
import { ApiIssue, apiIssue } from '../../core/api-errors';
import { Member, MembershipApplication } from '../../core/api.models';
import { AdminApplicationsService } from '../../core/membership.services';
import { ApiError, CalendarDatePipe, ConfirmDialog, Field, LoadingState, StatusBadge } from '../../shared/ui';
import { ageInYears, codepointMaxLength } from '../../shared/validation';

@Component({
  imports: [DatePipe, ReactiveFormsModule, RouterLink, ApiError, CalendarDatePipe, ConfirmDialog, Field, LoadingState, StatusBadge],
  template: `<nav class="breadcrumb" aria-label="Breadcrumb"><a routerLink="/admin/applications">Applications</a><span aria-hidden="true">/</span><span>Application details</span></nav>
    <app-api-error [issue]="issue()" [canRetry]="!saving()" (retry)="reload()" />
    @if (declined()) { <div class="alert alert--success" role="status">Application declined successfully.</div> }
    @if (loading()) { <app-loading text="Loading application details…" /> }
    @if (!loading() && application(); as person) {
      <div class="admin-page-heading detail-heading"><div><span class="section-tag">MEMBERSHIP APPLICATION</span><h1>{{ person.firstName }}<br />{{ person.lastName }}</h1><p>Submitted {{ person.submittedAtUtc | date:'d MMM yyyy, HH:mm':'+0200' }} SAST</p></div><app-status [status]="person.status" /></div>
      @if (createdMember(); as member) { <div class="alert alert--success" role="status"><strong>Membership approved. Member {{ member.memberNumber }} has been created.</strong><p>{{ confirmationEmailSent() ? "Confirmation email sent." : "The confirmation email could not be sent. The membership is still active." }}</p><p><a class="text-link" [routerLink]="['/admin/members', member.memberId]">View member {{ member.memberNumber }} →</a></p></div> }
      <div class="detail-layout"><section class="panel"><div class="detail-panel-title"><span class="eyebrow">THE APPLICANT</span><h2>THE DETAILS.</h2></div><dl class="details-grid"><div><dt>First name</dt><dd>{{ person.firstName }}</dd></div><div><dt>Surname</dt><dd>{{ person.lastName }}</dd></div><div><dt>Date of birth</dt><dd>{{ person.dateOfBirth | calendarDate }} @if (age(person.dateOfBirth) !== null) { <span class="muted">({{ age(person.dateOfBirth) }} years)</span> }</dd></div><div><dt>Phone</dt><dd>{{ person.phoneNumber }}</dd></div><div class="full-width"><dt>Email address</dt><dd>{{ person.email }}</dd></div><div class="full-width"><dt>Application reference</dt><dd class="reference-text">{{ person.applicationId }}</dd></div></dl>
        <div class="consent-summary"><h3>Application consent</h3><p><span aria-hidden="true">{{ person.termsAccepted ? '✓' : '○' }}</span> Membership terms {{ person.termsAccepted ? 'accepted' : 'not accepted' }}</p><p><span aria-hidden="true">{{ person.privacyConsent ? '✓' : '○' }}</span> Privacy consent {{ person.privacyConsent ? 'provided' : 'not provided' }}</p></div>
      </section>
      <section class="panel review-panel"><span class="eyebrow">THE NEXT STEP</span><h2>REVIEW APPLICATION.</h2>
        @if (person.status === 'Pending') { <p class="muted">Review the application before accepting or declining it.</p><app-field id="reviewNotes" label="Review notes (optional)" [control]="notes" [required]="false" hint="Up to 2,000 characters. Saved with the review."><textarea id="reviewNotes" [formControl]="notes" rows="5" placeholder="Add a short internal note…" aria-describedby="reviewNotes-hint reviewNotes-error" [attr.aria-invalid]="notes.touched && notes.invalid"></textarea></app-field><div class="review-actions"><button class="button" type="button" (click)="choose('accept')" [disabled]="saving() || conflicted()">Accept Application <span aria-hidden="true">✓</span></button><button class="button" type="button" (click)="choose('decline')" [disabled]="saving() || conflicted()">Decline Application</button></div><p class="review-caption">Accepting creates an active member and issues a member number.</p> }
        @else { <div class="decision-complete"><span class="state-mark" aria-hidden="true">{{ person.status === 'Approved' ? '✓' : '—' }}</span><h3>This registration is {{ person.status === 'Approved' ? 'approved' : 'declined' }}.</h3><p>This application has been reviewed. No further actions are available.</p></div> }
      </section></div>
    }
    @if (action()) { <app-confirm-dialog [title]="action() === 'accept' ? 'Accept membership application?' : 'Decline membership application?'" [message]="action() === 'accept' ? 'The applicant will become an active member and receive a membership number.' : 'The application will be declined and no member will be created.'" [confirmLabel]="action() === 'accept' ? 'Accept Application' : 'Decline Application'" [busy]="saving()" (cancel)="action.set(null)" (confirm)="decide()" /> }
  `,
})
export class ApplicationDetailPage {
  private readonly api = inject(AdminApplicationsService); private readonly route = inject(ActivatedRoute); private readonly destroy = inject(DestroyRef);
  private readonly refresh = new Subject<void>(); private id = '';
  readonly application = signal<MembershipApplication | null>(null); readonly loading = signal(true); readonly issue = signal<ApiIssue | null>(null);
  readonly saving = signal(false); readonly conflicted = signal(false); readonly action = signal<'accept' | 'decline' | null>(null);
  readonly createdMember = signal<Member | null>(null); 
  readonly declined = signal(false);
  readonly confirmationEmailSent = signal(false);
  readonly notes = new FormControl('', { nonNullable: true, validators: codepointMaxLength(2000) });
  readonly age = ageInYears;
  constructor() {
    combineLatest([this.route.paramMap, this.refresh.pipe(startWith(undefined))]).pipe(switchMap(([params]) => {
      this.id = params.get('applicationId') ?? ''; this.loading.set(true); this.issue.set(null); this.application.set(null); this.conflicted.set(false);
      this.createdMember.set(null); this.declined.set(false); this.action.set(null); this.notes.reset();
      return this.api.get(this.id).pipe(catchError(error => { this.issue.set(apiIssue(error)); return of(null); }), finalize(() => this.loading.set(false)));
    }), takeUntilDestroyed(this.destroy)).subscribe(person => {
      this.application.set(person);
      const decision = this.route.snapshot.queryParamMap.get('decision');
      if (person?.status === 'Pending' && (decision === 'accept' || decision === 'decline')) this.action.set(decision);
    });
  }
  reload(): void { if (!this.saving()) this.refresh.next(); }
  choose(action: 'accept' | 'decline'): void { this.notes.markAsTouched(); if (!this.notes.invalid && !this.saving() && !this.conflicted()) this.action.set(action); }
  decide(): void {
    const action = this.action(); if (!action || this.saving() || this.application()?.status !== 'Pending') return;
    this.saving.set(true); this.issue.set(null); const id = this.id;
    const request = this.notes.value.trim() ? { reviewNotes: this.notes.value.trim() } : {};
    const failure = (error: unknown) => {
      this.action.set(null); const issue = apiIssue(error, 'review'); this.issue.set(issue); this.conflicted.set(issue.status === 409);
      if (issue.fields['reviewNotes']) this.notes.setErrors({ server: issue.fields['reviewNotes'] });
    };
    if (action === 'accept') {
      this.api.approve(id, request).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.saving.set(false))).subscribe({
        next: result => { if (this.id !== id) return; this.action.set(null); this.createdMember.set(result.member); this.confirmationEmailSent.set(result.confirmationEmailSent); this.application.update(person => person ? { ...person, status: 'Approved' } : null); this.notes.reset(); }, error: failure,
      });
    }
    else {
      this.api.decline(id, request).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.saving.set(false))).subscribe({
        next: person => { if (this.id !== id) return; this.action.set(null); this.application.set(person); this.declined.set(true); this.notes.reset(); }, error: failure,
      });
    }
  }
}
