import { DatePipe } from "@angular/common";
import { Component, DestroyRef, inject, signal } from "@angular/core";
import { FormBuilder, ReactiveFormsModule, Validators } from "@angular/forms";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { Observable, finalize } from "rxjs";
import { Api } from "../core/api";
import { Auth } from "../core/auth";
import { Administrator } from "../core/models";
import { ListState } from "../core/list-state";
import {
  Issue,
  adminPassword,
  applyErrors,
  issueOf,
  maxText,
} from "../core/forms";
import { Field, IssueBox, Pager } from "../shared/ui";
@Component({
  imports: [ReactiveFormsModule, DatePipe, Field, IssueBox, Pager],
  template: `<div class="page-heading">
      <div>
        <span class="eyebrow">WORKSPACE ACCESS</span>
        <h1>User Management</h1>
        <p>Manage authorised organisation accounts.</p>
      </div>
      <button class="btn btn-sm btn-primary" (click)="openCreate()">
        + Add user
      </button>
      <button class="btn btn-sm btn-outline-secondary" (click)="changeOwn()">Change my password</button>
    </div>
    <ntm-issue [issue]="issue()" />
    @if (success()) {
      <div class="alert alert-success" role="status">{{ success() }}</div>
    }
    @if (mode()) {
      <section class="panel admin-editor">
        <div class="panel-title">
          <h2>
            {{
              mode() === "create"
                ? "Add administrator"
                : mode() === "edit"
                  ? "Update administrator"
                  : mode() === "own" ? "Change my password" : "Reset password"
            }}
          </h2>
          <button class="btn btn-sm btn-outline-secondary" (click)="close()">
            Close
          </button>
        </div>
        <form [formGroup]="form" (ngSubmit)="save()" novalidate>
          @if (mode() === "create" || mode() === "edit") {
            <ntm-field
              [form]="form"
              [field]="{
                key: 'email',
                label: 'Administrator email',
                type: 'email',
                max: 320,
                required: true,
              }"
            />
            <label class="form-label" for="userRole">Role</label>
            <select id="userRole" class="form-select form-select-sm" formControlName="role">
              <option value="TenantStaff">Admin</option>
              <option value="TenantAdmin">Organisation Administrator</option>
            </select>
          }
          @if (mode() === "own") {
            <label class="form-label" for="currentPassword">Current password</label>
            <input id="currentPassword" class="form-control form-control-sm" [type]="showPassword() ? 'text' : 'password'" formControlName="currentPassword" autocomplete="current-password" />
          }
          @if (mode() !== "edit") {
            <label class="form-label" for="password">{{ mode() === 'create' ? 'Initial password' : 'New password' }}</label>
            <input id="password" class="form-control form-control-sm" [type]="showPassword() ? 'text' : 'password'" formControlName="password" autocomplete="new-password" aria-describedby="password-policy" />
            <small id="password-policy">8–128 characters, at least one digit (0–9) and one special character. Spaces and letters are not special characters.</small>
            @if (form.controls.password.dirty && form.controls.password.invalid) { <p class="field-error" role="status">Check the password requirements above.</p> }
            <label class="form-label" for="confirmPassword">Confirm password</label>
            <input id="confirmPassword" class="form-control form-control-sm" [type]="showPassword() ? 'text' : 'password'" formControlName="confirmPassword" autocomplete="new-password" [attr.aria-invalid]="confirmationMismatch()" aria-describedby="confirm-password-error" />
            @if (confirmationMismatch()) { <p id="confirm-password-error" class="field-error" role="alert">Passwords do not match.</p> }
            <button type="button" class="btn btn-sm btn-link" (click)="showPassword.set(!showPassword())">{{ showPassword() ? 'Hide passwords' : 'Show passwords' }}</button>
          }
          <p class="text-secondary small">
            {{
              mode() === "edit"
                ? "Email changes invalidate existing sign-in sessions."
                : "Organisation Administrators can manage users. Admins manage veterans and businesses."
            }}
          </p>
          <button class="btn btn-sm btn-primary" [disabled]="saving() || form.invalid || confirmationMismatch()">
            {{ saving() ? "Saving…" : "Save administrator" }}
          </button>
        </form>
      </section>
    }
    <section class="panel">
      <form class="filter-bar" [formGroup]="filters" (ngSubmit)="search()">
        <div class="filter-search">
          <label for="adminSearch" class="visually-hidden"
            >Search administrator email</label
          ><input
            id="adminSearch"
            class="form-control form-control-sm"
            formControlName="search"
            maxlength="320"
            placeholder="Search email"
          />
        </div>
        <div>
          <label for="adminStatus" class="visually-hidden">Account status</label
          ><select
            id="adminStatus"
            class="form-select form-select-sm"
            formControlName="status"
          >
            <option value="">All accounts</option>
            <option value="active">Active</option>
            <option value="inactive">Inactive</option>
          </select>
        </div>
        <button class="btn btn-sm btn-primary">Search</button>
      </form>
      <ntm-issue [issue]="list.issue()" />
      @if (list.loading()) {
        <p class="empty-state" role="status">Loading administrators…</p>
      } @else if (list.data(); as page) {
        <div class="table-responsive">
          <table class="table align-middle">
            <thead>
              <tr>
                <th>Email</th>
                <th>Role</th>
                <th>Status</th>
                <th>Last sign-in</th>
                <th class="text-end">Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (admin of page.items; track admin.id) {
                <tr>
                  <td>
                    {{ admin.email }}
                    @if (admin.id === auth.actorId()) {
                      <span class="badge badge-neutral ms-2">You</span>
                    }
                  </td>
                  <td>{{ admin.role === 'TenantAdmin' ? 'Organisation Administrator' : admin.role === 'TenantStaff' ? 'Admin' : 'Platform Administrator' }}</td>
                  <td>
                    <span
                      class="badge"
                      [class.badge-green]="admin.isActive"
                      [class.badge-neutral]="!admin.isActive"
                      >{{ admin.isActive ? "Active" : "Inactive" }}</span
                    >
                  </td>
                  <td>
                    {{
                      admin.lastLoginAtUtc
                        ? (admin.lastLoginAtUtc | date: "dd MMM yyyy, HH:mm")
                        : "Never"
                    }}
                  </td>
                  <td class="text-end">
                    <div class="table-actions">
                      <button
                        class="btn btn-sm btn-outline-secondary"
                        (click)="edit(admin)"
                      >
                        Edit</button
                      ><button
                        class="btn btn-sm btn-outline-secondary"
                        [disabled]="admin.id === auth.actorId()"
                        (click)="password(admin)"
                      >
                        Reset password</button
                      ><button
                        class="btn btn-sm btn-outline-secondary"
                        [disabled]="saving() || admin.id === auth.actorId()"
                        (click)="status(admin)"
                      >
                        {{ admin.isActive ? "Deactivate" : "Activate" }}
                      </button>
                    </div>
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td class="empty-state" colspan="5">
                    No administrator accounts match these filters.
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <ntm-pager
          [page]="page.page"
          [pageSize]="page.pageSize"
          [total]="page.totalCount"
          (pageChange)="list.load({ page: $event })"
          (sizeChange)="list.size($event)"
        />
      }
    </section>`,
})
export class Administrators {
  private readonly api = inject(Api);
  readonly auth = inject(Auth);
  private readonly destroy = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);
  readonly list = new ListState(
    (query) => this.api.administrators(query),
    this.destroy,
  );
  readonly filters = this.fb.nonNullable.group({ search: [""], status: [""] });
  readonly form = this.fb.nonNullable.group({
    email: ["", [Validators.required, Validators.email, maxText(320)]],
    password: ["", adminPassword],
    confirmPassword: [""],
    currentPassword: [""],
    role: ["TenantStaff", Validators.required],
  });
  readonly mode = signal<"create" | "edit" | "password" | "own" | null>(null);
  readonly showPassword = signal(false);
  confirmationMismatch() { return this.mode() !== "edit" && this.form.controls.password.value !== this.form.controls.confirmPassword.value; }
  readonly selected = signal<Administrator | null>(null);
  readonly saving = signal(false);
  readonly issue = signal<Issue | null>(null);
  readonly success = signal("");
  constructor() {
    this.list.load();
  }
  hasUnsavedChanges() {
    return !!this.mode() && this.form.dirty;
  }
  private begin(
    mode: "create" | "edit" | "password" | "own",
    admin: Administrator | null,
  ) {
    if (this.saving()) return false;
    if (
      this.hasUnsavedChanges() &&
      !confirm("Discard the current administrator changes?")
    )
      return false;
    this.mode.set(mode);
    this.selected.set(admin);
    this.form.reset({ email: admin?.email ?? "", password: "", confirmPassword: "", currentPassword: "", role: admin?.role ?? "TenantStaff" });
    this.showPassword.set(false);
    this.form.controls.email.setValidators(
      mode === "password" || mode === "own"
        ? []
        : [Validators.required, Validators.email, maxText(320)],
    );
    this.form.controls.password.setValidators(
      mode === "edit" ? [] : adminPassword,
    );
    this.form.controls.confirmPassword.setValidators(mode === "edit" ? [] : [Validators.required]);
    this.form.controls.currentPassword.setValidators(mode === "own" ? [Validators.required] : []);
    this.form.updateValueAndValidity();
    this.form.controls.email.updateValueAndValidity();
    this.form.controls.password.updateValueAndValidity();
    this.form.controls.confirmPassword.updateValueAndValidity();
    this.form.controls.currentPassword.updateValueAndValidity();
    this.issue.set(null);
    this.success.set("");
    return true;
  }
  openCreate() {
    this.begin("create", null);
  }
  edit(admin: Administrator) {
    this.begin("edit", admin);
  }
  password(admin: Administrator) {
    this.begin("password", admin);
  }
  changeOwn() { this.begin("own", null); }
  close() {
    if (this.saving()) return;
    if (
      this.hasUnsavedChanges() &&
      !confirm("Discard the current administrator changes?")
    )
      return;
    this.form.reset();
    this.mode.set(null);
  }
  search() {
    const values = this.filters.getRawValue();
    this.list.load({
      page: 1,
      search: values.search,
      isActive: values.status === "" ? undefined : values.status === "active",
    });
  }
  save() {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid || this.confirmationMismatch()) return;
    this.issue.set(null);
    this.saving.set(true);
    const value = this.form.getRawValue();
    const mode = this.mode();
    const selected = this.selected();
    if ((mode === "create" || mode === "edit") && value.role === "TenantAdmin" && selected?.role !== "TenantAdmin" &&
        !confirm("Grant Organisation Administrator access? This user can manage other accounts.")) { this.saving.set(false); return; }
    const request: Observable<Administrator | void> =
      mode === "create"
        ? this.api.createAdministrator(value.email, value.password, value.role)
        : mode === "edit"
          ? this.api.updateAdministrator(selected!.id, value.email, value.role)
          : mode === "own" ? this.api.changeOwnPassword(value.currentPassword, value.password, value.confirmPassword)
          : this.api.resetPassword(selected!.id, value.password);
    request
      .pipe(
        takeUntilDestroyed(this.destroy),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: () => {
          this.form.reset();
          this.mode.set(null);
          if (mode === "own" || selected?.id === this.auth.actorId()) {
            this.auth.logout(false, mode === "own" ? "Your password was changed. Sign in again." : undefined);
            return;
          }
          this.success.set(
            mode === "password"
              ? "Password updated. Existing sessions are no longer valid."
              : "Administrator saved.",
          );
          this.list.load();
        },
        error: (e) => {
          this.issue.set(
            issueOf(
              e,
              "This change conflicts with an existing account or would remove the last active administrator.",
            ),
          );
          applyErrors(this.form, e);
          this.form.controls.password.reset();
          this.form.controls.confirmPassword.reset();
          this.form.controls.currentPassword.reset();
        },
      });
  }
  status(admin: Administrator) {
    if (
      this.saving() ||
      admin.id === this.auth.actorId() ||
      !confirm(`${admin.isActive ? "Deactivate" : "Activate"} ${admin.email}?`)
    )
      return;
    this.saving.set(true);
    this.issue.set(null);
    this.api
      .administratorStatus(admin.id, !admin.isActive)
      .pipe(
        takeUntilDestroyed(this.destroy),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: () => {
          this.success.set("Administrator status updated.");
          this.list.load();
        },
        error: (e) =>
          this.issue.set(
            issueOf(
              e,
              "The last active administrator cannot be deactivated. Check the current account status.",
            ),
          ),
      });
  }
}
