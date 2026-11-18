import { DestroyRef, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import {
  Observable,
  Subject,
  catchError,
  defer,
  finalize,
  of,
  switchMap,
} from "rxjs";
import { Page, Query } from "./models";
import { Issue, issueOf } from "./forms";
export class ListState<T> {
  readonly data = signal<Page<T> | null>(null);
  readonly loading = signal(false);
  readonly issue = signal<Issue | null>(null);
  readonly query = signal<Query>({ page: 1, pageSize: 20 });
  private readonly requests = new Subject<Query>();
  constructor(
    loader: (query: Query) => Observable<Page<T>>,
    destroy: DestroyRef,
  ) {
    this.requests
      .pipe(
        switchMap((query) =>
          defer(() => {
            this.loading.set(true);
            this.issue.set(null);
            return loader(query).pipe(
              catchError((error) => {
                this.issue.set(issueOf(error));
                return of(null);
              }),
              finalize(() => this.loading.set(false)),
            );
          }),
        ),
        takeUntilDestroyed(destroy),
      )
      .subscribe((data) => this.data.set(data));
  }
  load(changes: Query = {}) {
    const query = { ...this.query(), ...changes };
    this.query.set(query);
    this.requests.next(query);
  }
  size(pageSize: number) {
    this.load({ pageSize, page: 1 });
  }
}
