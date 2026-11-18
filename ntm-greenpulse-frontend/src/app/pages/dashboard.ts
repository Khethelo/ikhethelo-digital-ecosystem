import { CurrencyPipe, DecimalPipe } from "@angular/common";
import { Component, DestroyRef, inject, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { finalize } from "rxjs";
import { Api } from "../core/api";
import { Dashboard } from "../core/models";
import { Issue, issueOf } from "../core/forms";
import { IssueBox } from "../shared/ui";
@Component({
  imports: [CurrencyPipe, DecimalPipe, RouterLink, IssueBox],
  template: ` <div class="page-heading">
      <div>
        <span class="eyebrow">NETWORK AT A GLANCE</span>
        <h1>Overview</h1>
        <p>Veterans, businesses and recorded support needs.</p>
      </div>
      <div class="d-flex gap-2">
        <button
          class="btn btn-sm btn-outline-secondary"
          (click)="load()"
          [disabled]="loading()"
        >
          Refresh</button
        ><a class="btn btn-sm btn-primary" routerLink="/veterans/new"
          >+ Add veteran</a
        >
      </div>
    </div>
    <ntm-issue [issue]="issue()" />
    @if (loading()) {
      <div class="panel empty-state" role="status">
        Loading network statistics…
      </div>
    } @else if (data(); as d) {
      <div class="stats-grid">
        <a class="stat-card" routerLink="/veterans"
          ><span>Total veterans</span
          ><strong>{{ d.totalVeterans | number }}</strong
          ><small>Registered in the network</small></a
        ><a class="stat-card" routerLink="/businesses"
          ><span>Total businesses</span
          ><strong>{{ d.totalBusinesses | number }}</strong
          ><small>One business per veteran</small></a
        >
        <div class="stat-card">
          <span>Operational</span
          ><strong>{{ d.operationalBusinesses | number }}</strong
          ><small>Validated operational status</small>
        </div>
        <div class="stat-card accent">
          <span>Recorded funding amount</span
          ><strong class="money">{{
            d.totalRecordedFundingAmount | currency: "ZAR" : "R" : "1.2-2"
          }}</strong
          ><small
            >{{ d.fundingRequiredBusinesses }} businesses explicitly require
            funding</small
          >
        </div>
      </div>
      <div class="dashboard-grid">
        <section class="panel">
          <div class="panel-title">
            <h2>Business operations</h2>
            <a routerLink="/businesses">View businesses →</a>
          </div>
          <div class="status-summary">
            @for (item of statusRows(d); track item.label) {
              <div>
                <span
                  class="status-dot"
                  [class]="'status-dot ' + item.tone"
                ></span
                ><span>{{ item.label }}</span
                ><strong>{{ item.count | number }}</strong>
              </div>
            }
          </div>
          <p class="panel-note">
            Unrecorded status is shown separately from validated values.
          </p>
        </section>
        <section class="panel">
          <div class="panel-title">
            <h2>Businesses by sector</h2>
            <span>{{ d.businessesBySector.length }} recorded sectors</span>
          </div>
          @if (!d.businessesBySector.length) {
            <p class="empty-state">No sectors recorded yet.</p>
          }
          @for (sector of d.businessesBySector; track sector.value) {
            <div class="bar-row">
              <div>
                <span>{{ sector.value }}</span
                ><strong>{{ sector.count }}</strong>
              </div>
              <div class="bar-track">
                <span
                  [style.width.%]="(sector.count / sectorMax(d)) * 100"
                ></span>
              </div>
            </div>
          }
        </section>
        <section class="panel priority-panel">
          <div class="panel-title">
            <h2>Recorded priority needs</h2>
            <a routerLink="/businesses">View assessments →</a>
          </div>
          @if (!d.recordedPriorityNeeds.length) {
            <p class="empty-state">
              Priority needs will appear as assessments are recorded.
            </p>
          } @else {
            <div class="table-responsive">
              <table class="table table-sm mb-0">
                <thead>
                  <tr>
                    <th>Priority need</th>
                    <th class="text-end">Businesses</th>
                  </tr>
                </thead>
                <tbody>
                  @for (need of d.recordedPriorityNeeds; track need.value) {
                    <tr>
                      <td>{{ need.value }}</td>
                      <td class="text-end fw-semibold">{{ need.count }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </section>
      </div>
    } @else if (issue()) {
      <button class="btn btn-outline-secondary btn-sm" (click)="load()">
        Try again
      </button>
    }`,
})
export class DashboardPage {
  private readonly api = inject(Api);
  private readonly destroy = inject(DestroyRef);
  readonly data = signal<Dashboard | null>(null);
  readonly loading = signal(false);
  readonly issue = signal<Issue | null>(null);
  constructor() {
    this.load();
  }
  load() {
    if (this.loading()) return;
    this.loading.set(true);
    this.issue.set(null);
    this.api
      .dashboard()
      .pipe(
        takeUntilDestroyed(this.destroy),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: (data) => this.data.set(data),
        error: (error) => {
          this.data.set(null);
          this.issue.set(issueOf(error));
        },
      });
  }
  sectorMax(d: Dashboard) {
    return Math.max(1, ...d.businessesBySector.map((x) => x.count));
  }
  statusRows(d: Dashboard) {
    return [
      { label: "Operational", count: d.operationalBusinesses, tone: "green" },
      {
        label: "Partially operational",
        count: d.partiallyOperationalBusinesses,
        tone: "amber",
      },
      {
        label: "Not operational",
        count: d.notOperationalBusinesses,
        tone: "slate",
      },
      {
        label: "Not recorded",
        count: Math.max(
          0,
          d.totalBusinesses -
            d.operationalBusinesses -
            d.partiallyOperationalBusinesses -
            d.notOperationalBusinesses,
        ),
        tone: "muted",
      },
    ];
  }
}
