import { DestroyRef, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { Observable, Subject, catchError, combineLatest, finalize, of, startWith, switchMap } from 'rxjs';
import { ApiIssue, apiIssue } from '../core/api-errors';
import { ListQuery, Page } from '../core/api.models';
import { codepointMaxLength } from './validation';

/** One cancellable server-side query, with filters/pagination preserved in the route. */
export class CollectionState<T> {
  readonly loading = signal(true); readonly issue = signal<ApiIssue | null>(null); readonly data = signal<Page<T> | null>(null);
  readonly query = signal<ListQuery>({ page: 1, pageSize: 20 });
  readonly filters = new FormGroup({ search: new FormControl('', { nonNullable: true, validators: codepointMaxLength(320) }), status: new FormControl('all', { nonNullable: true }), pageSize: new FormControl(20, { nonNullable: true }) });
  private readonly refresh = new Subject<void>();
  constructor(private readonly route: ActivatedRoute, private readonly router: Router, destroy: DestroyRef,
    private readonly statuses: readonly string[], private readonly defaultStatus: string | undefined,
    loader: (query: ListQuery) => Observable<Page<T>>) {
    combineLatest([route.queryParamMap, this.refresh.pipe(startWith(undefined))]).pipe(
      switchMap(([params]) => {
        const query = this.parse(params); this.query.set(query);
        this.filters.setValue({ search: query.search ?? '', status: query.status ?? 'all', pageSize: query.pageSize }, { emitEvent: false });
        this.loading.set(true); this.issue.set(null); this.data.set(null);
        return loader(query).pipe(catchError(error => { this.issue.set(apiIssue(error)); return of(null); }), finalize(() => this.loading.set(false)));
      }), takeUntilDestroyed(destroy),
    ).subscribe(page => this.data.set(page));
  }
  applyFilters(): void {
    this.filters.markAllAsTouched(); if (this.filters.invalid) return;
    const values = this.filters.getRawValue();
    void this.router.navigate([], { relativeTo: this.route, queryParams: { page: 1, pageSize: values.pageSize, search: values.search.trim() || null, status: values.status }, queryParamsHandling: 'merge' });
  }
  page(page: number): void { void this.router.navigate([], { relativeTo: this.route, queryParams: { page }, queryParamsHandling: 'merge' }); }
  retry(): void { this.refresh.next(); }
  clear(): void {
    this.filters.reset({ search: '', status: this.defaultStatus ?? 'all', pageSize: 20 });
    void this.router.navigate([], { relativeTo: this.route, queryParams: this.defaultStatus ? { status: this.defaultStatus } : {} });
  }
  private parse(params: ParamMap): ListQuery {
    const page = Number(params.get('page') ?? 1); const pageSize = Number(params.get('pageSize') ?? 20);
    const requestedStatus = params.get('status') ?? this.defaultStatus ?? 'all';
    const status = requestedStatus === 'all' ? undefined : this.statuses.includes(requestedStatus) ? requestedStatus : this.defaultStatus;
    return { page: Number.isInteger(page) && page >= 1 && page <= 2147483647 ? page : 1, pageSize: [20, 50, 100].includes(pageSize) ? pageSize : 20, search: params.get('search')?.trim() || undefined, status };
  }
}
