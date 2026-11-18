import { Component, EventEmitter, Input, Output } from "@angular/core";
import { FormGroup, ReactiveFormsModule } from "@angular/forms";
import { Issue, errorText } from "../core/forms";

@Component({
  selector: "ntm-issue",
  template: `@if (issue) {
    <div class="alert alert-danger compact-alert" role="alert">
      <strong>{{ issue.message }}</strong>
      @if (issue.details.length) {
        <ul class="mb-0 mt-1">
          @for (detail of issue.details; track $index) {
            <li>{{ detail }}</li>
          }
        </ul>
      }
      @if (issue.traceId) {
        <small class="d-block mt-1">Reference: {{ issue.traceId }}</small>
      }
    </div>
  }`,
})
export class IssueBox {
  @Input() issue: Issue | null = null;
}

export interface FieldSpec {
  key: string;
  label: string;
  type?: string;
  max?: number;
  step?: string;
  min?: string;
  maxNumber?: string;
  hint?: string;
  wide?: boolean;
  source?: boolean;
  required?: boolean;
}
@Component({
  selector: "ntm-field",
  imports: [ReactiveFormsModule],
  template: `<div [formGroup]="form" class="field-wrap">
    <label class="form-label" [for]="field.key"
      >{{ field.label }}
      @if (field.required) {
        <span class="text-danger" aria-hidden="true">*</span>
      }
    </label>
    @if (field.type === "textarea") {
      <textarea
        class="form-control form-control-sm"
        [id]="field.key"
        [formControlName]="field.key"
        rows="3"
        [readOnly]="readonly"
        [attr.maxlength]="field.max ?? null"
        [attr.aria-required]="field.required ? 'true' : null"
        [attr.aria-invalid]="!!error()"
        [attr.aria-describedby]="field.key + '-help'"
      ></textarea>
    } @else if (field.type === "number") {
      <input
        type="number"
        class="form-control form-control-sm"
        [id]="field.key"
        [formControlName]="field.key"
        [readOnly]="readonly"
        [attr.step]="field.step ?? null"
        [attr.min]="field.min ?? null"
        [attr.max]="field.maxNumber ?? null"
        [attr.aria-required]="field.required ? 'true' : null"
        [attr.aria-invalid]="!!error()"
        [attr.aria-describedby]="field.key + '-help'"
      />
    } @else {
      <input
        class="form-control form-control-sm"
        [id]="field.key"
        [type]="field.type || 'text'"
        [formControlName]="field.key"
        [readOnly]="readonly"
        [attr.maxlength]="field.max ?? null"
        [attr.step]="field.step ?? null"
        [attr.min]="field.min ?? null"
        [attr.max]="field.maxNumber ?? null"
        [attr.aria-invalid]="!!error()"
        [attr.aria-describedby]="field.key + '-help'"
        [attr.autocomplete]="field.type === 'password' ? 'new-password' : 'off'"
      />
    }
    <div [id]="field.key + '-help'">
      @if (error()) {
        <span class="field-error" role="alert">{{ error() }}</span>
      } @else if (field.hint) {
        <small class="form-text">{{ field.hint }}</small>
      }
    </div>
  </div>`,
})
export class Field {
  @Input({ required: true }) form!: FormGroup;
  @Input({ required: true }) field!: FieldSpec;
  @Input() readonly = false;
  error() {
    return errorText(this.form.get(this.field.key));
  }
}

@Component({
  selector: "ntm-pager",
  template: `<div class="pager">
    <span aria-live="polite"
      >{{ total }} record{{ total === 1 ? "" : "s" }} · Page {{ page }} of
      {{ pages }}</span
    >
    <div class="d-flex align-items-center gap-2">
      <label for="pageSize" class="visually-hidden">Records per page</label
      ><select
        id="pageSize"
        class="form-select form-select-sm"
        [value]="pageSize"
        (change)="sizeChange.emit(+$any($event.target).value)"
        [disabled]="busy"
      >
        @for (size of [10, 20, 50, 100]; track size) {
          <option [value]="size" [selected]="size === pageSize">
            {{ size }} / page
          </option>
        }</select
      ><button
        class="btn btn-sm btn-outline-secondary"
        [disabled]="busy || page <= 1"
        (click)="pageChange.emit(page - 1)"
        aria-label="Previous page"
      >
        Previous</button
      ><button
        class="btn btn-sm btn-outline-secondary"
        [disabled]="busy || page >= pages"
        (click)="pageChange.emit(page + 1)"
        aria-label="Next page"
      >
        Next
      </button>
    </div>
  </div>`,
})
export class Pager {
  @Input() page = 1;
  @Input() pageSize = 20;
  @Input() total = 0;
  @Input() busy = false;
  @Output() pageChange = new EventEmitter<number>();
  @Output() sizeChange = new EventEmitter<number>();
  get pages() {
    return Math.max(1, Math.ceil(this.total / this.pageSize));
  }
}
