import { clubContent } from '../../content/club-content';
import { DatePipe } from '@angular/common';

import { Component, DestroyRef, ElementRef, ViewChild, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { ApiIssue, apiIssue, applyFieldErrors } from '../../core/api-errors';
import { SubmissionReceipt } from '../../core/api.models';
import { PublicMembershipService } from '../../core/membership.services';
import { ApiError, Field, StatusBadge } from '../../shared/ui';
import { adultDate, ageInYears, canonicalPhone, emailAddress, johannesburgToday, trimmedName } from '../../shared/validation';

@Component({
  imports: [ReactiveFormsModule, RouterLink, Field, ApiError, StatusBadge, DatePipe],
  template: `
    @if (receipt(); as result) {
      <section class="section receipt-section"><div class="receipt-card">
        <span class="receipt-mark" aria-hidden="true">✓</span><span class="section-tag">YOUR FIRST STEP IS COMPLETE</span>
        <h1 #receiptHeading tabindex="-1">REGISTRATION<br />RECEIVED.</h1><app-status [status]="result.status" />
        <p class="lead">{{ result.message }}</p><p>Thanks for registering with Es’hlahleni Social Club. Visit us in person and complete the R100 membership step. Once completed at the club, your membership will be activated and you’ll receive your member number.</p>
        <p>@for (line of content.contact.address; track line) { {{ line }}<br /> }</p><p><a class="button" [href]="content.contact.whatsAppUrl">Chat on WhatsApp · {{ content.contact.whatsAppNumber }}</a></p><div class="receipt-reference"><span>YOUR APPLICATION REFERENCE</span><code data-testid="application-reference">{{ result.applicationId }}</code><button type="button" class="text-button" (click)="copyReference()">{{ copied() ? 'Reference copied ✓' : 'Copy reference' }}</button></div>
        <p class="receipt-date">Submitted {{ result.submittedAtUtc | date:'d MMM yyyy, HH:mm':'+0200' }} SAST</p>
        <p><a class="button button--olive" [href]="confirmationWhatsAppUrl()" target="_blank" rel="noopener noreferrer">Contact the club about my registration</a></p>
        <p class="muted">Keep this reference for any enquiry with the club. There is no public application-tracking or member-login page.</p>
        <div class="button-group"><a class="button" routerLink="/">Back to the club ↗</a><a class="text-link" routerLink="/membership">What happens next →</a></div>
      </div></section>
    } @else {
      <section class="join-top"><div><span class="eyebrow">MEMBERSHIP / YOUR FIRST STEP</span><span>ES'HLAHLENI SOCIAL CLUB · 18+</span></div></section>
      <div class="join-layout">
        <aside class="join-guide"><span class="section-tag">MAKE YOURSELF AT HOME</span><h1>A PLACE<br />FOR YOU.</h1><p class="lead">Register online.<br />Then come through.</p><p>Share a few details to register. Visit the club to complete the R100 membership step. No account needed.</p>
          <ol class="join-progress"><li class="current"><span>01</span><div><strong>Your application</strong><small>Complete your details and consent.</small></div></li><li><span>02</span><div><strong>Visit the club</strong><small>Complete the R100 step in person.</small></div></li><li><span>03</span><div><strong>Membership activation</strong><small>Staff activate your membership and issue your member number.</small></div></li></ol>
          <div class="adult-note"><strong>18+ ONLY</strong><p>You must be at least 18 to register.</p></div><a class="text-link" routerLink="/faq">Questions before applying? →</a>
        </aside>
        <div class="join-form-card"><div class="join-form-header"><span class="eyebrow">LET’S GET INTRODUCED</span><h2>YOUR MEMBERSHIP<br />APPLICATION.</h2><p>Fields marked * are required.</p></div>
          <form [formGroup]="form" (ngSubmit)="submit()" novalidate class="join-form" [attr.aria-busy]="pending()">
            <app-api-error [issue]="issue()" />
            @if (invalidAttempt() && form.invalid) { <div class="alert alert--error" role="alert">Please check the highlighted fields before applying.</div> }
            <div class="form-section-title">01 / YOUR DETAILS</div>
            <div class="form-grid">
              <app-field id="firstName" label="First name" [control]="form.controls.firstName"><input id="firstName" formControlName="firstName" autocomplete="given-name" required placeholder="Your first name" [attr.aria-invalid]="invalid('firstName')" aria-describedby="firstName-error" /></app-field>
              <app-field id="lastName" label="Surname" [control]="form.controls.lastName"><input id="lastName" formControlName="lastName" autocomplete="family-name" required placeholder="Your surname" [attr.aria-invalid]="invalid('lastName')" aria-describedby="lastName-error" /></app-field>
              <app-field id="dateOfBirth" label="Date of birth" [control]="form.controls.dateOfBirth" hint="You must be at least 18 years old."><input id="dateOfBirth" type="date" formControlName="dateOfBirth" autocomplete="bday" required [max]="today" [attr.aria-invalid]="invalid('dateOfBirth')" aria-describedby="dateOfBirth-hint dateOfBirth-error" /></app-field>
              <app-field id="phoneNumber" label="Phone number" [control]="form.controls.phoneNumber" hint="International format, with no spaces."><input id="phoneNumber" type="tel" formControlName="phoneNumber" autocomplete="tel" required placeholder="+27821234567" [attr.aria-invalid]="invalid('phoneNumber')" aria-describedby="phoneNumber-hint phoneNumber-error" /></app-field>
              <app-field class="full-width" id="email" label="Email address" [control]="form.controls.email"><input id="email" type="email" formControlName="email" autocomplete="email" required placeholder="you@example.com" [attr.aria-invalid]="invalid('email')" aria-describedby="email-error" /></app-field>
            </div>
            @if (birthAge() !== null && !form.controls.dateOfBirth.invalid) { <p class="age-feedback" aria-live="polite">Age on today’s date: {{ birthAge() }} years. The minimum age is checked again when you apply.</p> }
            <div class="form-section-title consent-title">02 / YOUR CONSENT</div>
            <div class="alert alert--preview"><strong>Preview policy content</strong><p>The sample membership terms and privacy notice await the club’s approval before public launch.</p></div>
            <div class="checkbox-stack">
              <div><label class="checkbox-line" for="termsAccepted"><input id="termsAccepted" type="checkbox" formControlName="termsAccepted" required aria-describedby="termsAccepted-error" [attr.aria-invalid]="invalid('termsAccepted')" /><span>I accept the <a routerLink="/membership-terms" target="_blank" rel="noopener">Club Membership Terms <span class="visually-hidden">(opens in a new tab)</span></a>. *</span></label>@if (invalid('termsAccepted')) { <small id="termsAccepted-error" class="field-error" role="alert">Please read and accept the membership terms.</small> }</div>
              <div><label class="checkbox-line" for="privacyConsent"><input id="privacyConsent" type="checkbox" formControlName="privacyConsent" required aria-describedby="privacyConsent-error" [attr.aria-invalid]="invalid('privacyConsent')" /><span>I consent to the processing of my personal information for membership registration, as described in the <a routerLink="/privacy" target="_blank" rel="noopener">Privacy Notice <span class="visually-hidden">(opens in a new tab)</span></a>. *</span></label>@if (invalid('privacyConsent')) { <small id="privacyConsent-error" class="field-error" role="alert">Your privacy consent is required to apply.</small> }</div>
            </div>
            <button class="button button--full application-submit" type="submit" [disabled]="pending()">{{ pending() ? 'Submitting your application…' : 'Register for Membership' }} <span aria-hidden="true">↗</span></button>
            <p class="form-footnote">Your registration awaits in-person completion. Visit the club and complete the R100 step before staff activate your membership.</p>
          </form>
        </div>
      </div>
    }
  `,
})
export class JoinPage {
  readonly content = clubContent;
  readonly confirmationWhatsAppUrl = computed(() => {
    const receipt = this.receipt();
    if (!receipt) return this.content.contact.whatsAppUrl;
    const message = `Hi, I have a question about my registration with Es'hlahleni Social Club. My application reference is ${receipt.applicationId}.`;
    return `${this.content.contact.whatsAppUrl}?text=${encodeURIComponent(message)}`;
  });
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly api = inject(PublicMembershipService);
  private readonly destroy = inject(DestroyRef);
  private readonly element: ElementRef<HTMLElement> = inject(ElementRef);
  readonly form = this.fb.group({
    firstName: ['', [Validators.required, trimmedName]], lastName: ['', [Validators.required, trimmedName]],
    dateOfBirth: ['', [Validators.required, adultDate]], phoneNumber: ['', [Validators.required, canonicalPhone]],
    email: ['', [Validators.required, emailAddress]], termsAccepted: [false, Validators.requiredTrue], privacyConsent: [false, Validators.requiredTrue],
  });
  readonly today = johannesburgToday();
  private readonly birthValue = toSignal(this.form.controls.dateOfBirth.valueChanges, { initialValue: '' });
  readonly birthAge = computed(() => ageInYears(this.birthValue()));
  readonly pending = signal(false); readonly receipt = signal<SubmissionReceipt | null>(null);
  readonly issue = signal<ApiIssue | null>(null); readonly invalidAttempt = signal(false); readonly copied = signal(false);
  @ViewChild('receiptHeading') set receiptHeading(element: ElementRef<HTMLElement> | undefined) { element?.nativeElement.focus(); }
  invalid(name: keyof typeof this.form.controls): boolean { const control = this.form.controls[name]; return control.touched && control.invalid; }
  submit(): void {
    if (this.pending()) return;
    this.issue.set(null); this.form.markAllAsTouched(); this.invalidAttempt.set(this.form.invalid);
    if (this.form.invalid) { this.element.nativeElement.querySelector<HTMLElement>('input.ng-invalid')?.focus(); return; }
    this.pending.set(true);
    this.api.submit(this.form.getRawValue()).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.pending.set(false))).subscribe({
      next: receipt => { this.receipt.set(receipt); this.form.reset(); },
      error: error => { const issue = apiIssue(error, 'application'); this.issue.set(issue); applyFieldErrors(this.form, issue); },
    });
  }
  async copyReference(): Promise<void> {
    const id = this.receipt()?.applicationId;
    if (!id || !navigator.clipboard) return;
    try { await navigator.clipboard.writeText(id); this.copied.set(true); } catch { this.copied.set(false); }
  }
}
