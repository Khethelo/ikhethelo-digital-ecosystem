import { Component, DestroyRef, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { AuthService, safeAdminReturnUrl } from '../../core/auth.service';
import { ApiIssue, apiIssue, applyFieldErrors } from '../../core/api-errors';
import { ApiError, Field } from '../../shared/ui';
import { codepointMaxLength, emailAddress } from '../../shared/validation';
import { RetryCooldown } from '../../shared/retry-cooldown';

@Component({
  imports: [ReactiveFormsModule, RouterLink, ApiError, Field],
  template: `<a class="skip-link" href="#main-content">Skip to sign in</a><div class="login-layout">
    <aside class="login-brand"><a routerLink="/" class="login-back">← Back to the club</a><div class="login-brand-centre"><img src="assets/branding/eshlahleni-logo.jpeg" alt="Es’hlahleni Social Club emblem" width="340" height="380" /><h2>THE CLUB.<br />IN GOOD HANDS.</h2><p>ES'HLAHLENI SOCIAL CLUB · EST. 2025</p></div><span class="eyebrow">KUPHOLIWE AK’TATAZELWA!</span></aside>
    <main class="login-main" id="main-content" tabindex="-1"><div class="login-form-wrap"><span class="section-tag">CLUB ADMINISTRATION</span><h1>WELCOME<br />BACK.</h1><p class="muted">Sign in to complete registrations and manage members.</p>
      @if (auth.sessionMessage()) { <div class="alert" role="status">{{ auth.sessionMessage() }}</div> }
      <app-api-error [issue]="issue()" />
      <form [formGroup]="form" (ngSubmit)="submit()" novalidate [attr.aria-busy]="pending()">
        <app-field id="adminEmail" label="Email address" [control]="form.controls.email"><input id="adminEmail" type="email" formControlName="email" autocomplete="username" placeholder="Your administrator email" required aria-describedby="adminEmail-error" [attr.aria-invalid]="form.controls.email.touched && form.controls.email.invalid" /></app-field>
        <app-field id="adminPassword" label="Password" [control]="form.controls.password"><div class="password-input"><input id="adminPassword" [type]="showPassword() ? 'text' : 'password'" formControlName="password" autocomplete="current-password" placeholder="Your password" required aria-describedby="adminPassword-error" [attr.aria-invalid]="form.controls.password.touched && form.controls.password.invalid" /><button type="button" [attr.aria-pressed]="showPassword()" (click)="showPassword.set(!showPassword())">{{ showPassword() ? 'Hide' : 'Show' }}<span class="visually-hidden"> password</span></button></div></app-field>
        <button type="submit" class="button button--full" [disabled]="pending() || cooldown.remaining() > 0">{{ pending() ? 'Signing in…' : 'Sign in' }} <span aria-hidden="true">↗</span></button>
      </form><p class="login-note">For authorised club administrators only. Your session lasts up to 30 minutes. If you need access or a password reset, contact your club administrator.</p><a class="text-link" routerLink="/join">Looking to join the club? Apply here →</a>
    </div></main>
  </div>`,
})
export class LoginPage {
  readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder).nonNullable;
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy = inject(DestroyRef);
  readonly cooldown = new RetryCooldown(this.destroy);
  readonly pending = signal(false); readonly issue = signal<ApiIssue | null>(null); readonly showPassword = signal(false);
  readonly form = this.fb.group({ email: ['', [Validators.required, emailAddress]], password: ['', [Validators.required, codepointMaxLength(1024)]] });
  constructor() { if (this.auth.accessToken()) void this.router.navigateByUrl(safeAdminReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'))); }
  submit(): void {
    if (this.pending() || this.cooldown.remaining() > 0) return;
    this.form.markAllAsTouched(); this.issue.set(null);
    if (this.form.invalid) return;
    this.pending.set(true);
    this.auth.login(this.form.getRawValue()).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.pending.set(false))).subscribe({
      next: () => { this.form.controls.password.reset(); void this.router.navigateByUrl(safeAdminReturnUrl(this.route.snapshot.queryParamMap.get('returnUrl'))); },
      error: error => { const issue = apiIssue(error, 'login'); this.cooldown.start(issue.retryAfter); this.issue.set(issue); applyFieldErrors(this.form, issue); this.form.controls.password.reset(); },
    });
  }
}
