import { Component, DestroyRef, inject, signal } from "@angular/core";
import { FormBuilder, ReactiveFormsModule } from "@angular/forms";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { finalize, map, switchMap } from "rxjs";
import { Api } from "../core/api";
import { Business, BusinessWrite, STATUSES, Veteran } from "../core/models";
import {
  Issue,
  applyErrors,
  calendarDate,
  issueOf,
  maxText,
  numeric,
  requiredText,
} from "../core/forms";
import { ListState } from "../core/list-state";
import { Field, FieldSpec, IssueBox, Pager } from "../shared/ui";

@Component({
  imports: [ReactiveFormsModule, RouterLink, Field, IssueBox, Pager],
  template: ` <div class="page-heading">
      <div>
        <a class="breadcrumb-link" routerLink="/businesses">← Businesses</a>
        <h1>{{ id ? "Business & needs assessment" : "Add business" }}</h1>
        <p>
          Keep the current assessment and original workbook information
          together.
        </p>
      </div>
    </div>
    <ntm-issue [issue]="issue()" />
    @if (loading()) {
      <div class="panel empty-state" role="status">
        Loading business assessment…
      </div>
    } @else if (!id || record()) {
      <section class="panel owner-panel">
        <div class="panel-title">
          <h2>Veteran business owner</h2>
          <span>One business per veteran</span>
        </div>
        @if (selected(); as veteran) {
          <div class="owner-summary">
            <div>
              <a class="record-link" [routerLink]="['/veterans', veteran.id]">{{
                veteran.fullName
              }}</a
              ><small class="cell-note">{{
                veteran.mkmvaNumber || veteran.dmvNumber
              }}</small>
            </div>
            @if (veteran.business && veteran.business.id !== id) {
              <div class="alert alert-warning mb-0">
                This veteran already has a business.
                <a [routerLink]="['/businesses', veteran.business.id]"
                  >Open {{ veteran.business.businessName }}</a
                >
              </div>
            } @else {
              <span class="badge badge-green">{{
                id ? "Linked owner" : "Available for assignment"
              }}</span>
            }
            @if (!id) {
              <button
                class="btn btn-sm btn-outline-secondary"
                type="button"
                (click)="changeVeteran()"
              >
                Change veteran
              </button>
            }
          </div>
        } @else {
          <form
            class="filter-bar"
            [formGroup]="ownerSearch"
            (ngSubmit)="searchOwners()"
          >
            <div class="filter-search">
              <label for="ownerSearch" class="visually-hidden"
                >Search available veterans</label
              ><input
                id="ownerSearch"
                class="form-control form-control-sm"
                formControlName="search"
                placeholder="Search available veterans by name or registration"
                maxlength="200"
              />
            </div>
            <button class="btn btn-sm btn-outline-primary">
              Search veterans
            </button>
          </form>
          <ntm-issue [issue]="owners.issue()" />
          @if (owners.loading()) {
            <p role="status">Loading available veterans…</p>
          } @else if (owners.data(); as page) {
            <div class="owner-options">
              @for (veteran of page.items; track veteran.id) {
                <button
                  type="button"
                  class="owner-option"
                  (click)="choose(veteran.id)"
                >
                  <span
                    ><strong>{{ veteran.fullName }}</strong
                    ><small>{{
                      veteran.mkmvaNumber || veteran.dmvNumber
                    }}</small></span
                  ><span>Select →</span>
                </button>
              } @empty {
                <p class="empty-state">
                  No available veterans found.
                  <a routerLink="/veterans/new">Add a veteran</a> or adjust the
                  search.
                </p>
              }
            </div>
            <ntm-pager
              [page]="page.page"
              [pageSize]="page.pageSize"
              [total]="page.totalCount"
              [busy]="owners.loading()"
              (pageChange)="owners.load({ page: $event })"
              (sizeChange)="owners.size($event)"
            />
          }
        }
      </section>
      <form
        [formGroup]="form"
        (ngSubmit)="save()"
        novalidate
        [attr.aria-busy]="saving()"
      >
        <div class="assessment-layout">
          <div>
            <section class="panel">
              <div class="panel-title">
                <h2>Business profile</h2>
                <span>* Required</span>
              </div>
              <div class="form-grid">
                @for (field of profile; track field.key) {
                  <ntm-field
                    [class.field-wide]="field.wide"
                    [form]="form"
                    [field]="field"
                  />
                }
              </div>
            </section>
            <section class="panel">
              <div class="panel-title">
                <h2>Operations</h2>
                <span>Validated values</span>
              </div>
              <div class="form-grid">
                <div>
                  <label class="form-label" for="operationalStatus"
                    >Operational status</label
                  ><select
                    id="operationalStatus"
                    class="form-select form-select-sm"
                    formControlName="operationalStatus"
                  >
                    <option [ngValue]="null">Not recorded</option>
                    @for (status of statuses; track status) {
                      <option [ngValue]="status">{{ status }}</option>
                    }
                  </select>
                </div>
                @for (field of operations; track field.key) {
                  <ntm-field [form]="form" [field]="field" />
                }
              </div>
            </section>
            <section class="panel">
              <div class="panel-title">
                <h2>Needs & funding</h2>
                <span>Current assessment</span>
              </div>
              <div class="form-grid">
                @for (field of needs; track field.key) {
                  <ntm-field
                    [class.field-wide]="field.wide"
                    [form]="form"
                    [field]="field"
                  />
                }
                <div>
                  <label class="form-label" for="fundingRequired"
                    >Funding required?</label
                  ><select
                    id="fundingRequired"
                    class="form-select form-select-sm"
                    formControlName="fundingRequired"
                  >
                    <option [ngValue]="null">Not recorded</option>
                    <option [ngValue]="true">Yes</option>
                    <option [ngValue]="false">No</option>
                  </select>
                </div>
                <ntm-field
                  [form]="form"
                  [field]="{
                    key: 'fundingAmount',
                    label: 'Estimated funding amount (R)',
                    type: 'number',
                    step: '0.01',
                    min: '0',
                    maxNumber: '9999999999999.99',
                    hint: 'Rand. Leave blank when unknown; zero remains a recorded amount.',
                  }"
                />
              </div>
            </section>
            <section class="panel">
              <div class="panel-title"><h2>Assessment details</h2></div>
              <div class="form-grid">
                @for (field of assessment; track field.key) {
                  <ntm-field
                    [class.field-wide]="field.wide"
                    [form]="form"
                    [field]="field"
                  />
                }
              </div>
            </section>
          </div>
          <aside>
            <section class="panel source-panel">
              <div class="panel-title">
                <h2>Original workbook values</h2>
                <span class="badge badge-neutral">Source text</span>
              </div>
              <p class="text-secondary">
                Original descriptions are retained separately. They do not
                automatically change the validated values.
              </p>
              <div class="source-fields">
                @for (field of sourceFields; track field.key) {
                  <ntm-field [form]="form" [field]="field" [readonly]="!!id" />
                }
              </div>
            </section>
            <section class="panel guidance">
              <h2>Assessment notes</h2>
              <p>
                Keep employee qualifiers, uncertain funding entries and other
                original descriptions intact. Record confirmed values in the
                main form. Blank means unknown.
              </p>
            </section>
          </aside>
        </div>
        <div class="sticky-actions">
          <a class="btn btn-sm btn-outline-secondary" routerLink="/businesses"
            >Cancel</a
          ><span class="save-note">{{
            id
              ? "Update the existing business and assessment."
              : "Select an available veteran before saving."
          }}</span
          ><button
            class="btn btn-primary btn-sm"
            [disabled]="saving() || !canAssign()"
          >
            {{
              saving() ? "Saving…" : id ? "Save assessment" : "Create business"
            }}
          </button>
        </div>
      </form>
    }`,
})
export class BusinessForm {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroy = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);
  private selectionRevision = 0;
  readonly id = this.route.snapshot.paramMap.get("id");
  readonly record = signal<Business | null>(null);
  readonly selected = signal<Veteran | null>(null);
  readonly statuses = STATUSES;
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly issue = signal<Issue | null>(null);
  readonly owners = new ListState(
    (query) => this.api.veterans(query),
    this.destroy,
  );
  readonly ownerSearch = this.fb.nonNullable.group({ search: [""] });
  readonly form = this.fb.group({
    veteranId: ["", requiredText],
    businessName: ["", [requiredText, maxText(200)]],
    cipcRegistrationNumber: [null as string | null, maxText(100)],
    businessAddress: [null as string | null],
    sector: [null as string | null, maxText(200)],
    businessBackground: [null as string | null],
    operationalStatus: [null as BusinessWrite["operationalStatus"]],
    numberOfEmployees: [null as number | null, numeric(2147483647, 0)],
    yearsInOperation: [null as number | null, numeric(99999.9, 1)],
    currentBusinessNeeds: [null as string | null],
    priorityNeed: [null as string | null],
    fundingRequired: [null as boolean | null],
    fundingAmount: [null as number | null, numeric(9999999999999.99, 2)],
    additionalComments: [null as string | null],
    assessmentDate: [null as string | null, calendarDate],
    assessedBy: [null as string | null, maxText(200)],
    sourceRecordNumber: [null as number | null, numeric(2147483647, 0, 1)],
    operationalStatusSource: [null as string | null],
    numberOfEmployeesSource: [null as string | null],
    yearsInOperationSource: [null as string | null],
    fundingRequiredSource: [null as string | null],
  });
  readonly profile: FieldSpec[] = [
    {
      key: "businessName",
      label: "Business name",
      max: 200,
      required: true,
      wide: true,
    },
    {
      key: "cipcRegistrationNumber",
      label: "CIPC registration number",
      max: 100,
    },
    { key: "sector", label: "Sector", max: 200 },
    {
      key: "businessAddress",
      label: "Business address / area",
      type: "textarea",
      wide: true,
    },
    {
      key: "businessBackground",
      label: "Business background",
      type: "textarea",
      wide: true,
    },
  ];
  readonly operations: FieldSpec[] = [
    {
      key: "numberOfEmployees",
      label: "Number of employees",
      type: "number",
      step: "1",
      min: "0",
      maxNumber: "2147483647",
      hint: "Confirmed whole headcount, including the owner.",
    },
    {
      key: "yearsInOperation",
      label: "Years in operation",
      type: "number",
      step: "0.1",
      min: "0",
      maxNumber: "99999.9",
    },
  ];
  readonly needs: FieldSpec[] = [
    {
      key: "currentBusinessNeeds",
      label: "Current business needs",
      type: "textarea",
      wide: true,
    },
    {
      key: "priorityNeed",
      label: "Priority need",
      type: "textarea",
      wide: true,
      hint: "Record the most urgent need without discarding its description.",
    },
  ];
  readonly assessment: FieldSpec[] = [
    { key: "assessmentDate", label: "Assessment date", type: "date" },
    { key: "assessedBy", label: "Assessed by", max: 200 },
    {
      key: "additionalComments",
      label: "Additional comments / notes",
      type: "textarea",
      wide: true,
    },
  ];
  readonly sourceFields: FieldSpec[] = [
    {
      key: "sourceRecordNumber",
      label: "Workbook record number",
      type: "number",
      step: "1",
      min: "1",
    },
    {
      key: "operationalStatusSource",
      label: "Original operational status",
      type: "textarea",
    },
    {
      key: "numberOfEmployeesSource",
      label: "Original employee count / description",
      type: "textarea",
    },
    {
      key: "yearsInOperationSource",
      label: "Original years / description",
      type: "textarea",
    },
    {
      key: "fundingRequiredSource",
      label: "Original funding-required entry",
      type: "textarea",
    },
  ];
  constructor() {
    if (this.id) {
      this.loading.set(true);
      this.api
        .business(this.id)
        .pipe(
          switchMap((b) =>
            this.api.veteran(b.veteranId).pipe(map((v) => ({ b, v }))),
          ),
          takeUntilDestroyed(this.destroy),
          finalize(() => this.loading.set(false)),
        )
        .subscribe({
          next: ({ b, v }) => {
            this.record.set(b);
            this.selected.set(v);
            this.form.patchValue(b);
            this.form.markAsPristine();
          },
          error: (e) => this.issue.set(issueOf(e)),
        });
    } else {
      const veteranId = this.route.snapshot.queryParamMap.get("veteranId");
      if (veteranId) this.choose(veteranId);
      this.owners.load({ hasBusiness: false });
    }
  }
  searchOwners() {
    this.owners.load({
      page: 1,
      search: this.ownerSearch.controls.search.value,
      hasBusiness: false,
    });
  }
  choose(id: string, clearIssue = true) {
    const revision = ++this.selectionRevision;
    if (clearIssue) this.issue.set(null);
    this.selected.set(null);
    this.api
      .veteran(id)
      .pipe(takeUntilDestroyed(this.destroy))
      .subscribe({
        next: (v) => {
          if (revision !== this.selectionRevision) return;
          this.selected.set(v);
          this.form.controls.veteranId.setValue(v.id);
          this.form.markAsDirty();
        },
        error: (e) => this.issue.set(issueOf(e)),
      });
  }
  changeVeteran() {
    ++this.selectionRevision;
    this.selected.set(null);
    this.form.controls.veteranId.setValue("");
    this.owners.load({ hasBusiness: false, page: 1 });
  }
  canAssign() {
    const veteran = this.selected();
    return !!veteran && (!veteran.business || veteran.business.id === this.id);
  }
  hasUnsavedChanges() {
    return this.form.dirty;
  }
  save() {
    if (this.saving() || !this.canAssign()) return;
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    this.saving.set(true);
    this.issue.set(null);
    const values = this.form.getRawValue();
    const original = this.record();
    const body: BusinessWrite = {
      ...values,
      veteranId: values.veteranId!,
      businessName: values.businessName!,
      operationalStatus: values.operationalStatus ?? null,
      ...(original
        ? {
            sourceRecordNumber: original.sourceRecordNumber,
            operationalStatusSource: original.operationalStatusSource,
            numberOfEmployeesSource: original.numberOfEmployeesSource,
            yearsInOperationSource: original.yearsInOperationSource,
            fundingRequiredSource: original.fundingRequiredSource,
          }
        : {}),
    };
    (this.id
      ? this.api.updateBusiness(this.id, body)
      : this.api.createBusiness(body)
    )
      .pipe(
        takeUntilDestroyed(this.destroy),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: () => {
          this.form.markAsPristine();
          void this.router.navigate(["/businesses"]);
        },
        error: (e) => {
          const issue = issueOf(
            e,
            "This veteran already has a business. Your entered values have been kept. Refresh the veteran selection to open the existing record.",
          );
          this.issue.set(issue);
          applyErrors(this.form, e);
          if (issue.status === 409 && !this.id && this.selected())
            this.choose(this.selected()!.id, false);
        },
      });
  }
}
