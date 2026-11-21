import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { AdministratorsService } from '../../core/administrators.service';
import { AuthService } from '../../core/auth.service';
import { ApiIssue, apiIssue } from '../../core/api-errors';
import { adminPassword } from '../../shared/admin-password';
import { ApiError } from '../../shared/ui';

@Component({ imports: [ReactiveFormsModule, ApiError], template: `
  <div class="admin-page-heading admin-settings-heading"><div><span class="section-tag">MY ACCOUNT</span><h1>CHANGE PASSWORD.</h1></div></div>
  <section class="panel admin-form-panel"><app-api-error [issue]="issue()" /><form class="admin-form" [formGroup]="form" (ngSubmit)="save()">
    <div class="admin-form-field"><label for="currentPassword">Current password</label><input id="currentPassword" type="password" autocomplete="current-password" formControlName="currentPassword" />
    @if (issue()?.fields?.['currentPassword']; as errors) { <p class="field-error" role="alert">{{ errors[0] }}</p> }</div>
    <div class="admin-form-field"><label for="newPassword">New password</label><input id="newPassword" type="password" autocomplete="new-password" formControlName="newPassword" aria-describedby="passwordGuidance" />
    <p id="passwordGuidance" class="admin-form-hint">{{ guidance() }}</p>@if (issue()?.fields?.['password']; as errors) { <p class="field-error" role="alert">{{ errors[0] }}</p> }</div>
    <div class="admin-form-field"><label for="confirmPassword">Confirm new password</label><input id="confirmPassword" type="password" autocomplete="new-password" formControlName="confirmPassword" />
    @if (form.controls.confirmPassword.touched && form.controls.confirmPassword.value !== form.controls.newPassword.value) { <p class="field-error" role="alert">Passwords do not match.</p> }</div>
    <div class="admin-form-actions"><button class="button" type="submit" [disabled]="saving()">Change password</button></div>
  </form></section>` })
export class AccountPage {
  private readonly api = inject(AdministratorsService); private readonly auth = inject(AuthService);
  readonly guidance = signal('Password requirements are checked by the service.'); readonly saving = signal(false); readonly issue = signal<ApiIssue | null>(null);
  readonly form = inject(FormBuilder).nonNullable.group({ currentPassword: ['', Validators.required], newPassword: ['', adminPassword], confirmPassword: ['', Validators.required] });
  constructor() { this.api.passwordPolicy().subscribe({ next: policy => this.guidance.set(policy.guidance), error: () => {} }); }
  save(): void {
    this.form.markAllAsTouched(); const v = this.form.getRawValue();
    if (this.form.invalid || v.newPassword !== v.confirmPassword || this.saving()) return;
    this.saving.set(true); this.issue.set(null);
    this.api.changeOwn(v.currentPassword, v.newPassword, v.confirmPassword).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => { this.form.reset(); this.auth.logout(); this.auth.sessionMessage.set('Your password was changed. Sign in again.'); },
      error: error => { this.issue.set(apiIssue(error, 'password')); },
    });
  }
}
