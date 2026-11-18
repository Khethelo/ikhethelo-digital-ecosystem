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
        <h1>Administrators</h1>
        <p>Manage authorised tenant administrator accounts.</p>
      </div>
      <button class="btn btn-sm btn-primary" (click)="openCreate()">
        + Add administrator
      </button>
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
                  : "Reset password"
            }}
          </h2>
          <button class="btn btn-sm btn-outline-secondary" (click)="close()">
            Close
          </button>
        </div>
        <form [formGroup]="form" (ngSubmit)="save()" novalidate>
          @if (mode() !== "password") {
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
          }
          @if (mode() !== "edit") {
            <ntm-field
              [form]="form"
              [field]="{
                key: 'password',
                label:
                  mode() === 'password' ? 'New password' : 'Initial password',
                type: 'password',
                max: 128,
                required: true,
                hint: '12–128 characters. Share credentials through your trusted channel.',
              }"
            />
          }
          <p class="text-secondary small">
            {{
              mode() === "edit"
                ? "Email changes invalidate existing sign-in sessions."
                : "Only the TenantAdmin role can be assigned here."
            }}
          </p>
          <button class="btn btn-sm btn-primary" [disabled]="saving()">
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
                  <td>{{ admin.role }}</td>
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
  });
  readonly mode = signal<"create" | "edit" | "password" | null>(null);
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
    mode: "create" | "edit" | "password",
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
    this.form.reset({ email: admin?.email ?? "", password: "" });
    this.form.controls.email.setValidators(
      mode === "password"
        ? []
        : [Validators.required, Validators.email, maxText(320)],
    );
    this.form.controls.password.setValidators(
      mode === "edit" ? [] : adminPassword,
    );
    this.form.updateValueAndValidity();
    this.form.controls.email.updateValueAndValidity();
    this.form.controls.password.updateValueAndValidity();
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
    if (this.form.invalid) return;
    this.issue.set(null);
    this.saving.set(true);
    const value = this.form.getRawValue();
    const mode = this.mode();
    const selected = this.selected();
    const request: Observable<Administrator | void> =
      mode === "create"
        ? this.api.createAdministrator(value.email, value.password)
        : mode === "edit"
          ? this.api.updateAdministrator(selected!.id, value.email)
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
          if (selected?.id === this.auth.actorId()) {
            this.auth.logout();
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
