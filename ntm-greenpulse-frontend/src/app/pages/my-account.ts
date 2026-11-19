import { Component, DestroyRef, inject, signal } from "@angular/core";
import { FormBuilder, ReactiveFormsModule, Validators } from "@angular/forms";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { finalize } from "rxjs";
import { Api } from "../core/api";
import { Auth } from "../core/auth";
import { adminPassword, Issue, issueOf } from "../core/forms";
import { IssueBox } from "../shared/ui";

@Component({ imports: [ReactiveFormsModule, IssueBox], template: `
  <div class="page-heading"><div><h1>My Account</h1><p>Change your password.</p></div></div>
  <section class="panel admin-editor"><ntm-issue [issue]="issue()" />
    <form [formGroup]="form" (ngSubmit)="save()" novalidate>
      <label class="form-label" for="currentPassword">Current password</label>
      <input id="currentPassword" class="form-control form-control-sm" [type]="visible() ? 'text' : 'password'" formControlName="currentPassword" autocomplete="current-password">
      <label class="form-label" for="newPassword">New password</label>
      <input id="newPassword" class="form-control form-control-sm" [type]="visible() ? 'text' : 'password'" formControlName="newPassword" autocomplete="new-password" aria-describedby="password-policy">
      <small id="password-policy">8–128 characters, including a digit (0–9) and a special character that is not a letter, number or whitespace.</small>
      @if (form.controls.newPassword.dirty && form.controls.newPassword.invalid) { <p class="field-error" role="status">Check the password requirements.</p> }
      <label class="form-label" for="confirmPassword">Confirm new password</label>
      <input id="confirmPassword" class="form-control form-control-sm" [type]="visible() ? 'text' : 'password'" formControlName="confirmPassword" autocomplete="new-password" [attr.aria-invalid]="mismatch()">
      @if (mismatch()) { <p class="field-error" role="alert">Passwords do not match.</p> }
      <button type="button" class="btn btn-sm btn-link" (click)="visible.set(!visible())">{{ visible() ? 'Hide passwords' : 'Show passwords' }}</button>
      <button class="btn btn-sm btn-primary" [disabled]="pending() || form.invalid || mismatch()">{{ pending() ? 'Saving…' : 'Change password' }}</button>
    </form></section>` })
export class MyAccount {
  private readonly api = inject(Api);
  private readonly auth = inject(Auth);
  private readonly destroy = inject(DestroyRef);
  readonly visible = signal(false);
  readonly pending = signal(false);
  readonly issue = signal<Issue | null>(null);
  readonly form = inject(FormBuilder).nonNullable.group({ currentPassword: ["", Validators.required], newPassword: ["", adminPassword], confirmPassword: ["", Validators.required] });
  mismatch() { return this.form.controls.newPassword.value !== this.form.controls.confirmPassword.value; }
  save() {
    if (this.pending() || this.form.invalid || this.mismatch()) return;
    this.pending.set(true); this.issue.set(null);
    const v = this.form.getRawValue();
    this.api.changeOwnPassword(v.currentPassword, v.newPassword, v.confirmPassword).pipe(takeUntilDestroyed(this.destroy), finalize(() => this.pending.set(false))).subscribe({
      next: () => { this.form.reset(); this.auth.logout(false, "Your password was changed. Sign in again."); },
      error: error => { this.issue.set(issueOf(error)); this.form.reset(); },
    });
  }
}
