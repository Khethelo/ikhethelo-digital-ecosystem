import { DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize, forkJoin } from 'rxjs';
import { ApiIssue, apiIssue } from '../../core/api-errors';
import { Member, MembershipApplication, Page } from '../../core/api.models';
import { AdminApplicationsService, AdminMembersService } from '../../core/membership.services';
import { ApiError, LoadingState, StatusBadge } from '../../shared/ui';

interface Overview { pending: Page<MembershipApplication>; active: Page<Member>; members: Page<Member>; }
@Component({
  imports: [RouterLink, ApiError, LoadingState, StatusBadge, DatePipe],
  template: `<div class="admin-page-heading"><div><span class="section-tag">YOUR CLUB AT A GLANCE</span><h1>THE OVERVIEW.</h1><p>Registrations to complete. Members to look after.</p></div><span class="overview-date">{{ today | date:'EEEE, d MMMM yyyy':'+0200' }}</span></div>
    <app-api-error [issue]="issue()" [canRetry]="true" (retry)="load()" />
    @if (loading()) { <app-loading text="Loading your club overview…" /> }
    @if (!loading() && overview(); as data) {
      <div class="stats-grid"><a class="stat-card stat-card--olive" routerLink="/admin/applications" [queryParams]="{status:'AwaitingInPersonCompletion'}"><span class="stat-label">AWAITING IN-PERSON COMPLETION <span aria-hidden="true">↗</span></span><strong>{{ data.pending.totalCount }}</strong><span>Registrations awaiting a visit</span></a><a class="stat-card" routerLink="/admin/members" [queryParams]="{status:'Active'}"><span class="stat-label">ACTIVE MEMBERS <span aria-hidden="true">↗</span></span><strong>{{ data.active.totalCount }}</strong><span>Currently active memberships</span></a><a class="stat-card" routerLink="/admin/members"><span class="stat-label">ALL MEMBERS <span aria-hidden="true">↗</span></span><strong>{{ data.members.totalCount }}</strong><span>Across all membership statuses</span></a></div>
      <section class="table-panel"><div class="panel-heading"><div><span class="eyebrow">NEXT ON YOUR LIST</span><h2>AWAITING A VISIT.</h2></div><a class="text-link" routerLink="/admin/applications" [queryParams]="{status:'AwaitingInPersonCompletion'}">View applications ↗</a></div>
        @if (data.pending.items.length) { <table class="responsive-table"><caption class="visually-hidden">Newest pending membership applications</caption><thead><tr><th scope="col">APPLICANT</th><th scope="col">SUBMITTED</th><th scope="col">STATUS</th><th scope="col"><span class="visually-hidden">Action</span></th></tr></thead><tbody>@for (application of data.pending.items; track application.applicationId) { <tr><td data-label="Applicant"><strong>{{ application.firstName }} {{ application.lastName }}</strong><small>{{ application.email }}</small></td><td data-label="Submitted">{{ application.submittedAtUtc | date:'d MMM yyyy, HH:mm':'+0200' }}</td><td data-label="Status"><app-status [status]="application.status" /></td><td class="table-action"><a [routerLink]="['/admin/applications', application.applicationId]" [attr.aria-label]="'Review application for ' + application.firstName + ' ' + application.lastName">View registration ↗</a></td></tr> }</tbody></table> }
        @else { <div class="empty-state"><span class="state-mark" aria-hidden="true">✓</span><h3>You’re up to date.</h3><p>There are no registrations awaiting in-person completion right now.</p><a class="text-link" routerLink="/admin/applications" [queryParams]="{status:'all'}">Browse all applications →</a></div> }
      </section>
      <div class="admin-guidance"><span class="guidance-mark" aria-hidden="true">↗</span><div><strong>From application to member.</strong><p>After the R100 membership step is completed in person, activate the registration to create the Active member and issue their member number.</p></div><a routerLink="/admin/members">Browse members →</a></div>
    }`,
})
export class DashboardPage {
  private readonly applications = inject(AdminApplicationsService); private readonly members = inject(AdminMembersService); private readonly destroy = inject(DestroyRef);
  readonly today = new Date(); readonly loading = signal(false); readonly issue = signal<ApiIssue | null>(null); readonly overview = signal<Overview | null>(null);
  constructor() { this.load(); }
  load(): void {
    if (this.loading()) return;
    this.loading.set(true); this.issue.set(null); this.overview.set(null);
    forkJoin({ pending: this.applications.list({ page: 1, pageSize: 5, status: 'AwaitingInPersonCompletion' }), active: this.members.list({ page: 1, pageSize: 1, status: 'Active' }), members: this.members.list({ page: 1, pageSize: 1 }) })
      .pipe(takeUntilDestroyed(this.destroy), finalize(() => this.loading.set(false))).subscribe({ next: data => this.overview.set(data), error: error => this.issue.set(apiIssue(error)) });
  }
}
