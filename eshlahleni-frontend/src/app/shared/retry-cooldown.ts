import { DestroyRef, signal } from '@angular/core';

export class RetryCooldown {
  readonly remaining = signal(0);
  private timer?: ReturnType<typeof setInterval>;
  constructor(destroy: DestroyRef) { destroy.onDestroy(() => clearInterval(this.timer)); }
  start(seconds?: number): void {
    if (!seconds || seconds <= 0) return;
    const until = Date.now() + Math.min(seconds, 86400) * 1000;
    clearInterval(this.timer); this.remaining.set(Math.ceil((until - Date.now()) / 1000));
    this.timer = setInterval(() => { const left = Math.max(0, Math.ceil((until - Date.now()) / 1000)); this.remaining.set(left); if (!left) clearInterval(this.timer); }, 1000);
  }
}
