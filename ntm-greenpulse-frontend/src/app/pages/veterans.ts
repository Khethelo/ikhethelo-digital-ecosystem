import { Component, DestroyRef, inject } from "@angular/core";
import { FormBuilder, ReactiveFormsModule } from "@angular/forms";
import { RouterLink } from "@angular/router";
import { Api } from "../core/api";
import { ListState } from "../core/list-state";
import { IssueBox, Pager } from "../shared/ui";
@Component({
  imports: [ReactiveFormsModule, RouterLink, IssueBox, Pager],
  template: `<div class="page-heading">
      <div>
        <span class="eyebrow">NETWORK DIRECTORY</span>
        <h1>Veterans</h1>
        <p>Veteran identities, contact details and linked businesses.</p>
      </div>
      <a class="btn btn-primary btn-sm" routerLink="/veterans/new"
        >+ Add veteran</a
      >
    </div>
    <section class="panel">
      <form class="filter-bar" [formGroup]="filters" (ngSubmit)="search()">
        <div class="filter-search">
          <label for="veteranSearch" class="visually-hidden"
            >Search veterans</label
          ><input
            id="veteranSearch"
            class="form-control form-control-sm"
            formControlName="search"
            placeholder="Search name, MKMVA or DMV number"
            maxlength="200"
          />
        </div>
        <div>
          <label for="hasBusiness" class="visually-hidden"
            >Business assignment</label
          ><select
            id="hasBusiness"
            class="form-select form-select-sm"
            formControlName="hasBusiness"
          >
            <option value="">All veterans</option>
            <option value="yes">With a business</option>
            <option value="no">Without a business</option>
          </select>
        </div>
        <button class="btn btn-primary btn-sm">Search</button
        ><button
          type="button"
          class="btn btn-outline-secondary btn-sm"
          (click)="clear()"
        >
          Clear
        </button>
      </form>
      <ntm-issue [issue]="list.issue()" />
      @if (list.loading()) {
        <div class="empty-state" role="status">Loading veterans…</div>
      } @else if (list.data(); as page) {
        <div class="table-responsive">
          <table class="table table-hover align-middle">
            <thead>
              <tr>
                <th>Veteran</th>
                <th>Registration</th>
                <th>Contact</th>
                <th>Business</th>
                <th><span class="visually-hidden">Actions</span></th>
              </tr>
            </thead>
            <tbody>
              @for (v of page.items; track v.id) {
                <tr>
                  <td>
                    <a class="record-link" [routerLink]="['/veterans', v.id]">{{
                      v.fullName
                    }}</a>
                  </td>
                  <td>
                    <span>{{
                      v.mkmvaNumber ? "MKMVA: " + v.mkmvaNumber : "—"
                    }}</span
                    ><small class="cell-note">{{
                      v.dmvNumber ? "DMV: " + v.dmvNumber : ""
                    }}</small>
                  </td>
                  <td>
                    {{ v.contactNumber || "—"
                    }}<small class="cell-note">{{
                      v.emailAddress || ""
                    }}</small>
                  </td>
                  <td>
                    @if (v.business) {
                      <a [routerLink]="['/businesses', v.business.id]">{{
                        v.business.businessName
                      }}</a>
                    } @else {
                      <span class="badge badge-neutral">No business</span>
                    }
                  </td>
                  <td class="text-end">
                    <a
                      class="btn btn-sm btn-outline-secondary"
                      [routerLink]="['/veterans', v.id]"
                      >Open</a
                    >
                  </td>
                </tr>
              } @empty {
                <tr>
                  <td colspan="5" class="empty-state">
                    No veterans match these filters.
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
export class Veterans {
  private readonly api = inject(Api);
  readonly list = new ListState(
    (query) => this.api.veterans(query),
    inject(DestroyRef),
  );
  readonly filters = inject(FormBuilder).nonNullable.group({
    search: [""],
    hasBusiness: [""],
  });
  constructor() {
    this.list.load();
  }
  search() {
    const values = this.filters.getRawValue();
    this.list.load({
      page: 1,
      search: values.search,
      hasBusiness:
        values.hasBusiness === "" ? undefined : values.hasBusiness === "yes",
    });
  }
  clear() {
    this.filters.reset();
    this.search();
  }
}
