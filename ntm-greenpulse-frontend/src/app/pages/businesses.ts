import { CurrencyPipe } from "@angular/common";
import { Component, DestroyRef, inject } from "@angular/core";
import { FormBuilder, ReactiveFormsModule } from "@angular/forms";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { Api } from "../core/api";
import { ListState } from "../core/list-state";
import { STATUSES } from "../core/models";
import { IssueBox, Pager } from "../shared/ui";
@Component({
  imports: [ReactiveFormsModule, RouterLink, IssueBox, Pager, CurrencyPipe],
  template: `<div class="page-heading">
      <div>
        <span class="eyebrow">BUSINESS DIRECTORY</span>
        <h1>Businesses</h1>
        <p>Business profiles and current needs assessments.</p>
      </div>
      <a class="btn btn-primary btn-sm" routerLink="/businesses/new"
        >+ Add business</a
      >
    </div>
    <section class="panel">
      <form class="filter-bar wrap" [formGroup]="filters" (ngSubmit)="search()">
        <div class="filter-search">
          <label for="businessSearch" class="visually-hidden"
            >Search business name</label
          ><input
            id="businessSearch"
            class="form-control form-control-sm"
            formControlName="search"
            placeholder="Search business name"
            maxlength="200"
          />
        </div>
        <div>
          <label for="sectorFilter" class="visually-hidden">Sector</label
          ><input
            id="sectorFilter"
            class="form-control form-control-sm"
            formControlName="sector"
            placeholder="Exact sector"
            maxlength="200"
          />
        </div>
        <div>
          <label for="statusFilter" class="visually-hidden"
            >Operational status</label
          ><select
            id="statusFilter"
            class="form-select form-select-sm"
            formControlName="operationalStatus"
          >
            <option value="">All operational statuses</option>
            @for (status of statuses; track status) {
              <option [value]="status">{{ status }}</option>
            }
          </select>
        </div>
        <button class="btn btn-primary btn-sm">Search</button
        ><button
          type="button"
          class="btn btn-sm btn-outline-secondary"
          (click)="clear()"
        >
          Clear
        </button>
      </form>
      @if (veteranId) {
        <p class="filter-note">
          Filtered to a selected veteran.
          <a routerLink="/businesses">Show all businesses</a>
        </p>
      }
      <ntm-issue [issue]="list.issue()" />
      @if (list.loading()) {
        <div class="empty-state" role="status">Loading businesses…</div>
      } @else if (list.data(); as page) {
        <div class="table-responsive">
          <table class="table table-hover align-middle">
            <thead>
              <tr>
                <th>Business</th>
                <th>Sector</th>
                <th>Operations</th>
                <th class="text-end">Funding amount</th>
                <th>Priority need</th>
                <th><span class="visually-hidden">Actions</span></th>
              </tr>
            </thead>
            <tbody>
              @for (b of page.items; track b.id) {
                <tr>
                  <td>
                    <a
                      class="record-link"
                      [routerLink]="['/businesses', b.id]"
                      >{{ b.businessName }}</a
                    ><small class="cell-note">{{
                      b.cipcRegistrationNumber || "CIPC not recorded"
                    }}</small>
                  </td>
                  <td class="description-cell">{{ b.sector || "—" }}</td>
                  <td>
                    <span
                      class="badge"
                      [class.badge-green]="
                        b.operationalStatus === 'Operational'
                      "
                      [class.badge-amber]="
                        b.operationalStatus === 'Partially Operational'
                      "
                      [class.badge-neutral]="
                        !b.operationalStatus ||
                        b.operationalStatus === 'Not Operational'
                      "
                      >{{ b.operationalStatus || "Not recorded" }}</span
                    >
                  </td>
                  <td class="text-end text-nowrap">
                    {{
                      b.fundingAmount === null
                        ? "—"
                        : (b.fundingAmount | currency: "ZAR" : "R" : "1.2-2")
                    }}
                  </td>
                  <td class="description-cell">{{ b.priorityNeed || "—" }}</td>
                  <td class="text-end">
                    <a
                      class="btn btn-sm btn-outline-secondary"
                      [routerLink]="['/businesses', b.id]"
                      >Open</a
                    >
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="6" class="empty-state">
                    No businesses match these filters.
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
      } @else if (list.issue()) {
        <button class="btn btn-sm btn-outline-secondary" (click)="list.load()">
          Try again
        </button>
      }
    </section>`,
})
export class Businesses {
  private readonly api = inject(Api);
  readonly statuses = STATUSES;
  readonly veteranId =
    inject(ActivatedRoute).snapshot.queryParamMap.get("veteranId") || undefined;
  readonly list = new ListState(
    (query) => this.api.businesses(query),
    inject(DestroyRef),
  );
  readonly filters = inject(FormBuilder).nonNullable.group({
    search: [""],
    sector: [""],
    operationalStatus: [""],
  });
  constructor() {
    this.list.load({ veteranId: this.veteranId });
  }
  search() {
    this.list.load({ ...this.filters.getRawValue(), page: 1 });
  }
  clear() {
    this.filters.reset();
    this.search();
  }
}
