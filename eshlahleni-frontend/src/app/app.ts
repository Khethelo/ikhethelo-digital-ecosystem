import { DOCUMENT } from '@angular/common';
import { Component, DestroyRef, Injector, afterNextRender, inject } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';

@Component({
  imports: [RouterOutlet],
  selector: 'app-root',
  styleUrl: './app.scss',
  template: '<router-outlet />',
})
export class App {
  constructor() {
    const document = inject(DOCUMENT); const injector = inject(Injector);
    inject(Router).events.pipe(filter(event => event instanceof NavigationEnd), takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => afterNextRender(() => document.getElementById('main-content')?.focus({ preventScroll: true }), { injector }));
  }
}
