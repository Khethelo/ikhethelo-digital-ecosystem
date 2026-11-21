import { AfterViewInit, ChangeDetectionStrategy, Component, ElementRef, EventEmitter, Input, OnDestroy, Output, ViewChild } from '@angular/core';
import { AbstractControl } from '@angular/forms';
import { Pipe, PipeTransform } from '@angular/core';
import { ApiIssue } from '../core/api-errors';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-status',
  template: `<span [class]="'status status--' + status.toLowerCase()"><span class="status-dot" aria-hidden="true"></span>{{ status === 'Pending' ? 'Pending' : status === 'Approved' ? 'Approved' : status === 'Rejected' ? 'Declined' : status }}</span>`,
})
export class StatusBadge { @Input({ required: true }) status = ''; }

@Component({
  selector: 'app-loading',
  template: `<div class="loading-state" role="status"><span class="spinner" aria-hidden="true"></span><span>{{ text }}</span></div>`,
})
export class LoadingState { @Input() text = 'Loading…'; }

@Component({
  selector: 'app-api-error',
  imports: [RouterLink],
  template: `@if (issue) {
    <div class="alert alert--error" role="alert" tabindex="-1">
      <strong>{{ issue.message }}</strong>
      @if (issue.retryAfter) { <p>Please wait at least {{ issue.retryAfter }} seconds before retrying.</p> }
      @for (message of generalErrors; track $index) { <p>{{ message }}</p> }
      @if (issue.traceId) { <small>Support reference: <span class="break-anywhere">{{ issue.traceId }}</span></small> }
      @if (issue.existingApplicationId) { <p><a class="button" [routerLink]="['/admin/applications', issue.existingApplicationId]">Review Existing Application</a></p> }
      @if (canRetry) { <button class="text-button" type="button" (click)="retry.emit()">Try again <span aria-hidden="true">↻</span></button> }
    </div>
  }`,
})
export class ApiError {
  @Input() issue: ApiIssue | null = null;
  @Input() canRetry = false;
  @Output() retry = new EventEmitter<void>();
  get generalErrors(): string[] { return this.issue?.fields['request'] ?? []; }
}

@Component({
  selector: 'app-field',
  changeDetection: ChangeDetectionStrategy.Default,
  host: { '[attr.id]': 'null' },
  template: `<div class="field" [class.field--invalid]="!!message">
    <label [for]="id">{{ label }} @if (required) { <span aria-hidden="true">*</span> }</label>
    <ng-content />
    @if (hint) { <small class="field-hint" [id]="id + '-hint'">{{ hint }}</small> }
    @if (message) { <small class="field-error" [id]="id + '-error'" role="alert">{{ message }}</small> }
  </div>`,
})
export class Field {
  @Input({ required: true }) id = '';
  @Input({ required: true }) label = '';
  @Input({ required: true }) control: AbstractControl | null = null;
  @Input() hint = '';
  @Input() required = true;
  get message(): string {
    const control = this.control;
    if (!control?.touched || !control.errors) return '';
    const errors = control.errors;
    if (errors['server']) return errors['server'].join(' ');
    if (errors['name']) return 'Use 2–100 characters, excluding surrounding spaces.';
    if (errors['email']) return 'Enter a valid email address of at most 320 characters.';
    if (errors['phone']) return 'Use international format, for example +27821234567, without spaces.';
    if (errors['date']) return 'Enter a valid date of birth that is not in the future.';
    if (errors['adult']) return 'You must be at least 18 years old to apply.';
    if (errors['maxlength'] || errors['length']) return 'The information is too long. Please shorten it.';
    return 'Please complete this field.';
  }
}

@Component({
  selector: 'app-pagination',
  template: `<nav class="pagination" aria-label="Pagination">
    <p>Showing <strong>{{ start }}–{{ end }}</strong> of <strong>{{ totalCount }}</strong></p>
    <div class="pagination-actions">
      <button class="button button--small button--outline" type="button" [disabled]="busy || page <= 1" (click)="pageChange.emit(page - 1)" aria-label="Previous page">← Previous</button>
      <span class="page-number" aria-live="polite">{{ page }} / {{ lastPage }}</span>
      <button class="button button--small button--outline" type="button" [disabled]="busy || page >= lastPage" (click)="pageChange.emit(page + 1)" aria-label="Next page">Next →</button>
    </div>
  </nav>`,
})
export class Pagination {
  @Input() page = 1; @Input() pageSize = 20; @Input() totalCount = 0; @Input() busy = false;
  @Output() pageChange = new EventEmitter<number>();
  get lastPage(): number { return Math.max(1, Math.ceil(this.totalCount / this.pageSize)); }
  get start(): number { return this.totalCount ? (this.page - 1) * this.pageSize + 1 : 0; }
  get end(): number { return Math.min(this.page * this.pageSize, this.totalCount); }
}

@Component({
  selector: 'app-confirm-dialog',
  template: `<dialog #dialog class="confirm-dialog" aria-labelledby="confirmation-title" aria-describedby="confirmation-message" (cancel)="cancelDialog($event)">
    <div class="dialog-label">PLEASE CONFIRM</div>
    <h2 id="confirmation-title">{{ title }}</h2>
    <p id="confirmation-message">{{ message }}</p>
    <div class="dialog-actions">
      <button class="button button--outline" type="button" autofocus [disabled]="busy" (click)="cancel.emit()">Cancel</button>
      <button class="button" [class.button--danger]="danger" type="button" [disabled]="busy" (click)="confirm.emit()">{{ busy ? 'Saving…' : confirmLabel }}</button>
    </div>
  </dialog>`,
})
export class ConfirmDialog implements AfterViewInit, OnDestroy {
  @ViewChild('dialog', { static: true }) dialog!: ElementRef<HTMLDialogElement>;
  @Input({ required: true }) title = ''; @Input({ required: true }) message = '';
  @Input() confirmLabel = 'Confirm'; @Input() busy = false; @Input() danger = false;
  @Output() confirm = new EventEmitter<void>(); @Output() cancel = new EventEmitter<void>();
  ngAfterViewInit(): void { this.dialog.nativeElement.showModal(); }
  ngOnDestroy(): void { this.dialog.nativeElement.close(); }
  cancelDialog(event: Event): void { event.preventDefault(); if (!this.busy) this.cancel.emit(); }
}

@Pipe({ name: 'calendarDate' })
export class CalendarDatePipe implements PipeTransform {
  transform(value: string | null | undefined): string {
    if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return '—';
    const [year, month, day] = value.split('-').map(Number);
    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    return `${day} ${months[month - 1]} ${year}`;
  }
}
