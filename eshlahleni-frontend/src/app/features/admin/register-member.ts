import { Component, DestroyRef, ElementRef, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { ApiIssue, apiIssue, applyFieldErrors } from '../../core/api-errors';
import { Member } from '../../core/api.models';
import { AdminMembersService } from '../../core/membership.services';
import { ApiError, Field } from '../../shared/ui';
import { adultDate, canonicalPhone, emailAddress, johannesburgToday, trimmedName } from '../../shared/validation';

@Component({ imports: [ReactiveFormsModule, RouterLink, Field, ApiError], template: `
  <div class="admin-page-heading admin-settings-heading"><div><span class="section-tag">MEMBERSHIP / WALK-IN</span><h1>REGISTER MEMBER.</h1><p>Register a member joining at the club.</p></div></div>
  @if (result(); as done) {
    <section class="panel admin-form-panel" role="status" aria-live="polite"><h2>MEMBER REGISTERED.</h2><p>Member number</p><p class="registration-number">{{ done.member.memberNumber }}</p>
      @if (done.confirmationEmailSent) { <p>Confirmation email sent to {{ done.member.email }}.</p> }
      @else { <p>The confirmation email could not be sent. The membership is still active.</p> }
      <div class="admin-form-actions"><a class="button" [routerLink]="['/admin/members', done.member.memberId]">View Member</a><button class="admin-form-cancel" type="button" (click)="another()">Register Another Member</button><a class="text-link" routerLink="/admin/members">Back to Members</a></div>
    </section>
  } @else {
    <section class="panel admin-form-panel"><h2>MEMBER DETAILS.</h2><form class="admin-form" [formGroup]="form" (ngSubmit)="submit()" novalidate [attr.aria-busy]="saving()">
      <app-api-error [issue]="issue()" />
      <app-field id="firstName" label="First name" [control]="form.controls.firstName"><input id="firstName" formControlName="firstName" autocomplete="given-name" required aria-describedby="firstName-error" [attr.aria-invalid]="invalid('firstName')" /></app-field>
      <app-field id="lastName" label="Surname" [control]="form.controls.lastName"><input id="lastName" formControlName="lastName" autocomplete="family-name" required aria-describedby="lastName-error" [attr.aria-invalid]="invalid('lastName')" /></app-field>
      <app-field id="dateOfBirth" label="Date of birth" [control]="form.controls.dateOfBirth" hint="Member must be at least 18 years old."><input id="dateOfBirth" type="date" formControlName="dateOfBirth" required [max]="today" aria-describedby="dateOfBirth-hint dateOfBirth-error" [attr.aria-invalid]="invalid('dateOfBirth')" /></app-field>
      <app-field id="phoneNumber" label="Phone number" [control]="form.controls.phoneNumber" hint="Enter 10 digits, for example 0843190994."><input id="phoneNumber" type="tel" formControlName="phoneNumber" autocomplete="tel" required placeholder="0843190994" aria-describedby="phoneNumber-hint phoneNumber-error" [attr.aria-invalid]="invalid('phoneNumber')" /></app-field>
      <app-field id="email" label="Email address" [control]="form.controls.email"><input id="email" type="email" formControlName="email" autocomplete="email" required aria-describedby="email-error" [attr.aria-invalid]="invalid('email')" /></app-field>
      <div class="checkbox-stack"><div><label class="checkbox-line"><input type="checkbox" formControlName="termsAccepted" [attr.aria-invalid]="invalid('termsAccepted')" aria-describedby="termsAccepted-error" /><span>Member accepted the membership terms</span></label>@if (invalid('termsAccepted')) { <small id="termsAccepted-error" class="field-error" role="alert">Membership terms acceptance is required.</small> }</div>
      <div><label class="checkbox-line"><input type="checkbox" formControlName="privacyConsent" [attr.aria-invalid]="invalid('privacyConsent')" aria-describedby="privacyConsent-error" /><span>Member provided privacy consent</span></label>@if (invalid('privacyConsent')) { <small id="privacyConsent-error" class="field-error" role="alert">Privacy consent is required.</small> }</div></div>
      <div class="admin-form-actions"><button class="button" type="submit" [disabled]="saving()">{{ saving() ? 'Registering…' : 'Register Member' }}</button><a class="text-link" routerLink="/admin/members">Back to Members</a></div>
    </form></section>
  }` })
export class RegisterMemberPage {
  private readonly api = inject(AdminMembersService); private readonly destroy = inject(DestroyRef); private readonly element: ElementRef<HTMLElement> = inject(ElementRef);
  private readonly fb = inject(FormBuilder).nonNullable;
  readonly form = this.fb.group({ firstName: ['', [Validators.required, trimmedName]], lastName: ['', [Validators.required, trimmedName]],
    dateOfBirth: ['', [Validators.required, adultDate]], phoneNumber: ['', [Validators.required, canonicalPhone]], email: ['', [Validators.required, emailAddress]],
    termsAccepted: [false, Validators.requiredTrue], privacyConsent: [false, Validators.requiredTrue] });
  readonly today = johannesburgToday(); readonly saving = signal(false); readonly issue = signal<ApiIssue | null>(null);
  readonly result = signal<{ member: Member; confirmationEmailSent: boolean } | null>(null);
  invalid(name: keyof typeof this.form.controls): boolean { const c = this.form.controls[name]; return c.touched && c.invalid; }
  submit(): void {
    if (this.saving()) return; this.issue.set(null); this.form.markAllAsTouched();
    if (this.form.invalid) { this.element.nativeElement.querySelector<HTMLElement>('input.ng-invalid')?.focus(); return; }
    this.saving.set(true); this.api.register(this.form.getRawValue()).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.saving.set(false))).subscribe({
      next: result => this.result.set(result), error: error => { const issue = apiIssue(error, 'registration'); this.issue.set(issue); applyFieldErrors(this.form, issue); }
    });
  }
  another(): void { this.form.reset(); this.issue.set(null); this.result.set(null); }
}
