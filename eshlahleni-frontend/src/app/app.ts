import { DOCUMENT } from '@angular/common';
import { Component, DestroyRef, Injector, afterNextRender, inject } from '@angular/core';
import { NavigationEnd, NavigationError, Router, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { UnexpectedErrors } from './core/unexpected-errors';

@Component({
  imports: [RouterOutlet],
  selector: 'app-root',
  styleUrl: './app.scss',
  template: `@if (errors.message()) { <div class="alert alert--error" role="alert">{{ errors.message() }} <button class="text-button" type="button" (click)="errors.message.set('')">Dismiss</button></div> }<router-outlet />`,
})
export class App {
  readonly errors = inject(UnexpectedErrors);
  constructor() {
    const document = inject(DOCUMENT); const injector = inject(Injector);
    inject(Router).events.pipe(filter(event => event instanceof NavigationEnd), takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => afterNextRender(() => document.getElementById('main-content')?.focus({ preventScroll: true }), { injector }));
    inject(Router).events.pipe(filter(event => event instanceof NavigationError), takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => this.errors.message.set('We couldn’t load this page. Please refresh and try again.'));
  }
}
