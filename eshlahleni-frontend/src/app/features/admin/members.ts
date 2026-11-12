import { DatePipe } from '@angular/common';
import { Component, DestroyRef, inject } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Member, memberStatuses } from '../../core/api.models';
import { AdminMembersService } from '../../core/membership.services';
import { CollectionState } from '../../shared/collection-state';
import { ApiError, LoadingState, Pagination, StatusBadge } from '../../shared/ui';

@Component({
  imports: [ReactiveFormsModule, RouterLink, DatePipe, ApiError, LoadingState, Pagination, StatusBadge],
  template: `<div class="admin-page-heading"><div><span class="section-tag">MEMBERSHIP / THE COMMUNITY</span><h1>THE PEOPLE<br />BEHIND THE CLUB.</h1><p>Find a member, check their details, and keep their membership up to date.</p></div><span class="admin-count">{{ state.data()?.totalCount ?? '—' }} <small>matching members</small></span></div>
    <section class="table-panel"><form class="filter-bar" [formGroup]="state.filters" (ngSubmit)="state.applyFilters()"><div class="filter-search"><label for="memberSearch">Search members</label><input id="memberSearch" type="search" formControlName="search" placeholder="Search name, contact or member number…" />@if (state.filters.controls.search.invalid && state.filters.controls.search.touched) { <small class="field-error" role="alert">Use at most 320 characters.</small> }</div><div><label for="memberStatus">Membership status</label><select id="memberStatus" formControlName="status"><option value="all">All statuses</option>@for (status of statuses; track status) { <option [value]="status">{{ status }}</option> }</select></div><div class="filter-page-size"><label for="memberPageSize">Per page</label><select id="memberPageSize" formControlName="pageSize"><option [ngValue]="20">20</option><option [ngValue]="50">50</option><option [ngValue]="100">100</option></select></div><button class="button" type="submit" [disabled]="state.loading()">Search <span aria-hidden="true">↗</span></button><button class="text-button filter-clear" type="button" (click)="state.clear()" [disabled]="state.loading()">Reset</button></form>
      <div class="table-state-area"><app-api-error [issue]="state.issue()" [canRetry]="true" (retry)="state.retry()" />@if (state.loading()) { <app-loading text="Loading members…" /> }
        @if (!state.loading() && state.data(); as page) {
          @if (page.items.length) { <table class="responsive-table"><caption class="visually-hidden">Members matching the selected filters</caption><thead><tr><th scope="col">MEMBER</th><th scope="col">MEMBER NO.</th><th scope="col">PHONE</th><th scope="col">JOINED</th><th scope="col">STATUS</th><th scope="col"><span class="visually-hidden">Action</span></th></tr></thead><tbody>@for (member of page.items; track member.memberId) { <tr><td data-label="Member"><strong>{{ member.firstName }} {{ member.lastName }}</strong><small>{{ member.email }}</small></td><td data-label="Member number"><span class="member-number">{{ member.memberNumber }}</span></td><td data-label="Phone">{{ member.phoneNumber }}</td><td data-label="Joined">{{ member.joinedAtUtc | date:'d MMM yyyy':'+0200' }}</td><td data-label="Status"><app-status [status]="member.status" /></td><td class="table-action"><a [routerLink]="['/admin/members', member.memberId]" [attr.aria-label]="'View member ' + member.firstName + ' ' + member.lastName">View details ↗</a></td></tr> }</tbody></table> }
          @else { <div class="empty-state"><span class="state-mark" aria-hidden="true">◎</span><h3>No members found.</h3><p>Try another search or status. Activating registrations creates member records.</p><a class="text-link" routerLink="/admin/applications">View registrations →</a></div> }
          <app-pagination [page]="page.page" [pageSize]="page.pageSize" [totalCount]="page.totalCount" [busy]="state.loading()" (pageChange)="state.page($event)" />
        }
      </div>
    </section>`,
})
export class MembersPage {
  private readonly api = inject(AdminMembersService);
  readonly statuses = memberStatuses;
  readonly state = new CollectionState<Member>(inject(ActivatedRoute), inject(Router), inject(DestroyRef), memberStatuses, undefined, query => this.api.list(query));
}
