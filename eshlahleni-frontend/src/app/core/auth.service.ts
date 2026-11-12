import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { API_CONFIG, endpoint } from './api.config';
import { LoginRequest, TokenResponse } from './api.models';

export function safeAdminReturnUrl(value: string | null | undefined): string {
  return value && /^\/admin(?:\/|$)/.test(value) && !value.startsWith('/admin/login') && !/[\\\r\n]/.test(value)
    ? value : '/admin';
}
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly config = inject(API_CONFIG);
  private readonly token = signal<string | null>(null);
  private readonly expiresAt = signal(0);
  private expiryTimer?: ReturnType<typeof setTimeout>;
  private revision = 0;
  readonly authenticated = computed(() => this.token() !== null);
  readonly sessionMessage = signal('');
  constructor() { inject(DestroyRef).onDestroy(() => clearTimeout(this.expiryTimer)); }

  login(request: LoginRequest) {
    const attempt = ++this.revision;
    return this.http.post<TokenResponse>(endpoint(this.config.apiBaseUrl, '/auth/login'), {
      email: request.email.trim(), password: request.password,
    }).pipe(tap(response => {
      if (attempt !== this.revision) throw new Error('Sign-in attempt no longer active.');
      if (!response.accessToken || response.tokenType !== 'Bearer' || response.expiresIn !== 900) throw new Error('Unexpected authentication response.');
      clearTimeout(this.expiryTimer);
      this.token.set(response.accessToken);
      this.expiresAt.set(Date.now() + response.expiresIn * 1000);
      this.sessionMessage.set('');
      this.expiryTimer = setTimeout(() => this.logout(true), response.expiresIn * 1000);
    }));
  }
  accessToken(): string | null {
    if (this.token() && Date.now() >= this.expiresAt()) { this.logout(true); return null; }
    return this.token();
  }
  logout(expired = false): void {
    ++this.revision;
    clearTimeout(this.expiryTimer);
    this.token.set(null); this.expiresAt.set(0);
    this.sessionMessage.set(expired ? 'Your session has ended. Please sign in again.' : 'You have signed out.');
    const current = this.router.url;
    const returnUrl = expired ? safeAdminReturnUrl(current) : '/admin';
    void this.router.navigate(['/admin/login'], { queryParams: returnUrl === '/admin' ? {} : { returnUrl } });
  }
}
