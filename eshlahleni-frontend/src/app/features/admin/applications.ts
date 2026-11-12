import { DatePipe } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MembershipApplication, applicationStatuses } from '../../core/api.models';
import { AdminApplicationsService } from '../../core/membership.services';
import { CollectionState } from '../../shared/collection-state';
import { ApiError, LoadingState, Pagination, StatusBadge } from '../../shared/ui';

@Component({
  imports: [ReactiveFormsModule, RouterLink, DatePipe, ApiError, LoadingState, Pagination, StatusBadge],
  template: `<div class="admin-page-heading"><div><span class="section-tag">MEMBERSHIP / APPLICATIONS</span><h1>THE NEXT CHAPTER<br />STARTS HERE.</h1><p>Find a registration and activate membership after the R100 step is completed in person.</p></div><span class="admin-count">{{ state.data()?.totalCount ?? '—' }} <small>matching applications</small></span></div>
    <section class="table-panel"><form class="filter-bar" [formGroup]="state.filters" (ngSubmit)="state.applyFilters()"><div class="filter-search"><label for="applicationSearch">Search applications</label><input id="applicationSearch" type="search" formControlName="search" placeholder="Search name, email or phone…" />@if (state.filters.controls.search.invalid && state.filters.controls.search.touched) { <small class="field-error" role="alert">Use at most 320 characters.</small> }</div><div><label for="applicationStatus">Application status</label><select id="applicationStatus" formControlName="status"><option value="all">All statuses</option>@for (status of statuses; track status) { <option [value]="status">{{ status === 'AwaitingInPersonCompletion' ? 'Awaiting In-Person Completion' : status === 'Completed' ? 'Completed Registrations' : 'Closed (historical)' }}</option> }</select></div><div class="filter-page-size"><label for="applicationPageSize">Per page</label><select id="applicationPageSize" formControlName="pageSize"><option [ngValue]="20">20</option><option [ngValue]="50">50</option><option [ngValue]="100">100</option></select></div><button class="button" type="submit" [disabled]="state.loading()">Search <span aria-hidden="true">↗</span></button><button class="text-button filter-clear" type="button" (click)="state.clear()" [disabled]="state.loading()">Reset</button></form>
      <div class="table-state-area"><app-api-error [issue]="state.issue()" [canRetry]="true" (retry)="state.retry()" />@if (state.loading()) { <app-loading text="Loading applications…" /> }
        @if (!state.loading() && state.data(); as page) {
          @if (page.items.length) { <table class="responsive-table"><caption class="visually-hidden">Membership applications matching the selected filters</caption><thead><tr><th scope="col">APPLICANT</th><th scope="col">PHONE</th><th scope="col">SUBMITTED</th><th scope="col">STATUS</th><th scope="col"><span class="visually-hidden">Action</span></th></tr></thead><tbody>@for (application of page.items; track application.applicationId) { <tr><td data-label="Applicant"><strong>{{ application.firstName }} {{ application.lastName }}</strong><small>{{ application.email }}</small></td><td data-label="Phone">{{ application.phoneNumber }}</td><td data-label="Submitted">{{ application.submittedAtUtc | date:'d MMM yyyy, HH:mm':'+0200' }}</td><td data-label="Status"><app-status [status]="application.status" /></td><td class="table-action"><a [routerLink]="['/admin/applications', application.applicationId]" [attr.aria-label]="'View application for ' + application.firstName + ' ' + application.lastName">View details ↗</a></td></tr> }</tbody></table> }
          @else { <div class="empty-state"><span class="state-mark" aria-hidden="true">○</span><h3>No applications found.</h3><p>There are no applications matching these filters. Try a different name or status.</p><button class="text-button" type="button" (click)="state.clear()">Reset filters →</button></div> }
          <app-pagination [page]="page.page" [pageSize]="page.pageSize" [totalCount]="page.totalCount" [busy]="state.loading()" (pageChange)="state.page($event)" />
        }
      </div>
    </section>`,
})
export class ApplicationsPage {
  private readonly api = inject(AdminApplicationsService);
  readonly statuses = applicationStatuses;
  readonly state = new CollectionState<MembershipApplication>(inject(ActivatedRoute), inject(Router), inject(DestroyRef), applicationStatuses, 'AwaitingInPersonCompletion', query => this.api.list(query));
}
