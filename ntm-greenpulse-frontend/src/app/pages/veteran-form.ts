import { Component, DestroyRef, inject, signal } from "@angular/core";
import { FormBuilder, ReactiveFormsModule } from "@angular/forms";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { finalize } from "rxjs";
import { Api } from "../core/api";
import { Veteran } from "../core/models";
import {
  Issue,
  applyErrors,
  issueOf,
  maxText,
  registration,
  requiredText,
} from "../core/forms";
import { Field, FieldSpec, IssueBox } from "../shared/ui";
@Component({
  imports: [ReactiveFormsModule, RouterLink, Field, IssueBox],
  template: `<div class="page-heading">
      <div>
        <a class="breadcrumb-link" routerLink="/veterans">← Veterans</a>
        <h1>{{ id ? "Veteran details" : "Add veteran" }}</h1>
        <p>Record identity and contact information. A business is optional.</p>
      </div>
    </div>
    <ntm-issue [issue]="issue()" />
    @if (loading()) {
      <div class="panel empty-state" role="status">Loading veteran…</div>
    } @else if (!id || record()) {
      <div class="form-layout">
        <form
          class="panel"
          [formGroup]="form"
          (ngSubmit)="save()"
          novalidate
          [attr.aria-busy]="saving()"
        >
          <div class="panel-title">
            <h2>Veteran information</h2>
            <span>* Required</span>
          </div>
          <div class="form-grid">
            @for (field of fields; track field.key) {
              <ntm-field
                [class.field-wide]="field.wide"
                [form]="form"
                [field]="field"
                [readonly]="!!field.source && !!id"
              />
            }
          </div>
          @if (form.touched && form.hasError("registration")) {
            <p class="field-error" role="alert">
              Provide at least one actual MKMVA or DMV registration number.
            </p>
          }
          <div class="form-actions">
            <a class="btn btn-sm btn-outline-secondary" routerLink="/veterans"
              >Cancel</a
            ><button class="btn btn-primary btn-sm" [disabled]="saving()">
              {{
                saving() ? "Saving…" : id ? "Save changes" : "Create veteran"
              }}
            </button>
          </div>
        </form>
        <aside>
          <section class="panel">
            <h2>Linked business</h2>
            @if (record()?.business; as business) {
              <span class="badge badge-green mb-2">Business assigned</span>
              <h3 class="fs-6">{{ business.businessName }}</h3>
              <p class="text-secondary">
                This veteran already has a business. Open the existing
                assessment to update it.
              </p>
              <a
                class="btn btn-sm btn-outline-primary"
                [routerLink]="['/businesses', business.id]"
                >Open business</a
              >
            } @else {
              <p class="text-secondary">
                No business is linked to this veteran.
              </p>
              @if (id) {
                <a
                  class="btn btn-sm btn-outline-primary"
                  routerLink="/businesses/new"
                  [queryParams]="{ veteranId: id }"
                  >Add business</a
                >
              } @else {
                <small class="text-secondary"
                  >Save the veteran before adding a business.</small
                >
              }
            }
          </section>
          <section class="panel guidance">
            <h2>Registration details</h2>
            <p>
              Either an actual MKMVA or DMV number is enough. Generic references
              such as “DMV Database” are not registration numbers.
            </p>
          </section>
        </aside>
      </div>
    }`,
})
export class VeteranForm {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly destroy = inject(DestroyRef);
  readonly id = inject(ActivatedRoute).snapshot.paramMap.get("id");
  readonly record = signal<Veteran | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly issue = signal<Issue | null>(null);
  readonly form = inject(FormBuilder).nonNullable.group(
    {
      fullName: ["", [requiredText, maxText(200)]],
      mkmvaNumber: ["", maxText(100)],
      dmvNumber: ["", maxText(100)],
      contactNumber: ["", maxText(50)],
      emailAddress: ["", maxText(320)],
    },
    { validators: registration },
  );
  readonly fields: FieldSpec[] = [
    {
      key: "fullName",
      label: "Full name",
      max: 200,
      required: true,
      wide: true,
    },
    { key: "mkmvaNumber", label: "MKMVA registration number", max: 100 },
    { key: "dmvNumber", label: "DMV registration number", max: 100 },
    { key: "contactNumber", label: "Contact number", max: 50 },
    {
      key: "emailAddress",
      label: "Recorded email / contact text",
      max: 320,
      hint: "Original contact text is retained; this is not an account.",
    },
  ];
  constructor() {
    if (this.id) {
      this.loading.set(true);
      this.api
        .veteran(this.id)
        .pipe(
          takeUntilDestroyed(this.destroy),
          finalize(() => this.loading.set(false)),
        )
        .subscribe({
          next: (v) => {
            this.record.set(v);
            this.form.patchValue({
              fullName: v.fullName,
              mkmvaNumber: v.mkmvaNumber ?? "",
              dmvNumber: v.dmvNumber ?? "",
              contactNumber: v.contactNumber ?? "",
              emailAddress: v.emailAddress ?? "",
            });
            this.form.markAsPristine();
          },
          error: (e) => this.issue.set(issueOf(e)),
        });
    }
  }
  hasUnsavedChanges() {
    return this.form.dirty;
  }
  save() {
    if (this.saving()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.issue.set(null);
    this.saving.set(true);
    const values = this.form.getRawValue();
    // Existing nullable source references retain their exact original value on full PUT.
    const body = {
      ...values,
      membershipReference: this.id
        ? this.record()!.membershipReference
        : null,
    };
    (this.id
      ? this.api.updateVeteran(this.id, body)
      : this.api.createVeteran(body)
    )
      .pipe(
        takeUntilDestroyed(this.destroy),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: (v) => {
          this.form.markAsPristine();
          this.record.set(v);
          void this.router.navigate(["/veterans"]);
        },
        error: (e) => {
          this.issue.set(
            issueOf(
              e,
              "An MKMVA or DMV number is already assigned to another veteran. Check the registration details.",
            ),
          );
          applyErrors(this.form, e);
        },
      });
  }
}
