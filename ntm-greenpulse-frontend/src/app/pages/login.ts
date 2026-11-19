import { Component, DestroyRef, inject, signal } from "@angular/core";
import { FormBuilder, ReactiveFormsModule, Validators } from "@angular/forms";
import { ActivatedRoute, Router } from "@angular/router";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { finalize, switchMap, tap } from "rxjs";
import { Auth, safeReturn } from "../core/auth";
import { Api } from "../core/api";
import { Issue, applyErrors, issueOf, maxText } from "../core/forms";
import { Field, IssueBox } from "../shared/ui";

@Component({
  imports: [ReactiveFormsModule, Field, IssueBox],
  template: `<div class="login-page">
    <div class="login-intro">
      <a class="brand" href="/login"
        ><span class="brand-mark">gp</span
        ><span
          >NTM <strong>GreenPulse</strong
          ><small>NETWORK ADMINISTRATION</small></span
        ></a
      >
      <div>
        <span class="eyebrow">INNOBIZ DUT PROGRAMME</span>
        <h1>Stronger businesses.<br />Shared progress.</h1>
        <p>
          A focused workspace for supporting veteran entrepreneurs and
          understanding their business needs.
        </p>
      </div>
      <small>NTM GreenPulse Network</small>
    </div>
    <main class="login-panel" id="main-content">
      <div class="login-card">
        <span class="eyebrow">ADMINISTRATOR ACCESS</span>
        <h2>Welcome back</h2>
        <p class="text-secondary">
          Sign in to manage veterans and their businesses.
        </p>
        @if (auth.message()) {
          <div class="alert alert-info" role="status">{{ auth.message() }}</div>
        }
        <ntm-issue [issue]="issue()" />
        <form
          [formGroup]="form"
          (ngSubmit)="submit()"
          novalidate
          [attr.aria-busy]="pending()"
        >
          <ntm-field
            [form]="form"
            [field]="{
              key: 'email',
              label: 'Email address',
              type: 'email',
              required: true,
              max: 320,
            }"
          />
          <div class="mb-3">
            <label class="form-label" for="password">Password</label
            ><input
              class="form-control"
              id="password"
              type="password"
              formControlName="password"
              autocomplete="current-password"
              required
              maxlength="1024"
            />
          </div>
          <button class="btn btn-primary w-100" [disabled]="pending()">
            {{ pending() ? "Signing in…" : "Sign in" }}
          </button>
        </form>
        <p class="login-note">
          For authorised NTM administrators. Contact your administrator for
          account access or a password reset. Your session lasts 30 minutes.
        </p>
      </div>
    </main>
  </div>`,
})
export class Login {
  readonly auth = inject(Auth);
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy = inject(DestroyRef);
  readonly form = inject(FormBuilder).nonNullable.group({
    email: ["", [Validators.required, Validators.email, maxText(320)]],
    password: ["", [Validators.required, maxText(1024)]],
  });
  readonly pending = signal(false);
  readonly issue = signal<Issue | null>(null);
  submit() {
    if (this.pending()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.pending.set(true);
    this.issue.set(null);
    const value = this.form.getRawValue();
    let authenticated = false;
    this.auth
      .login(value.email, value.password)
      .pipe(
        tap(() => (authenticated = true)),
        switchMap(() => this.api.dashboard()),
        takeUntilDestroyed(this.destroy),
        finalize(() => this.pending.set(false)),
      )
      .subscribe({
        next: () => {
          this.form.controls.password.reset();
          void this.router.navigateByUrl(
            safeReturn(this.route.snapshot.queryParamMap.get("returnUrl")),
          );
        },
        error: (error) => {
          this.auth.clear();
          const issue = issueOf(error);
          this.issue.set(!authenticated && issue.status === 401
            ? { ...issue, message: "Email or password is incorrect, or your account is inactive. Please check your details and try again.", traceId: undefined }
            : issue);
          applyErrors(this.form, error);
          this.form.controls.password.reset();
        },
      });
  }
}
