import { HttpClient } from '@angular/common/http';
import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { API_CONFIG, endpoint } from './api.config';
import { LoginRequest, TokenResponse } from './api.models';
import { DOCUMENT } from '@angular/common';

const sessionKey = 'eshlahleni.admin.session';

export function safeAdminReturnUrl(value: string | null | undefined): string {
  return value && /^\/admin(?:\/|$)/.test(value) && !value.startsWith('/admin/login') && !/[\\\r\n]/.test(value)
    ? value : '/admin';
}
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly config = inject(API_CONFIG);
  private readonly document = inject(DOCUMENT);
  private readonly token = signal<string | null>(null);
  private readonly expiresAt = signal(0);
  private expiryTimer?: ReturnType<typeof setTimeout>;
  private revision = 0;
  readonly authenticated = computed(() => this.token() !== null);
  readonly role = signal<string | null>(null);
  readonly actorId = signal<string | null>(null);
  readonly sessionMessage = signal('');
  readonly accessDeniedMessage = signal('');
  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.expiryTimer));
    this.restore();
  }

  private storage(): Storage | null {
    try { return this.document.defaultView?.sessionStorage ?? null; } catch { return null; }
  }
  private claims(token: string): { role?: string; sub?: string; exp?: number } {
    try { return JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'))); } catch { return {}; }
  }
  private setSession(token: string, expiry: number): void {
    const claims = this.claims(token);
    this.token.set(token); this.role.set(typeof claims.role === 'string' ? claims.role : null);
    this.actorId.set(typeof claims.sub === 'string' ? claims.sub : null); this.expiresAt.set(expiry);
    clearTimeout(this.expiryTimer);
    this.expiryTimer = setTimeout(() => this.logout(true), Math.max(0, expiry - Date.now()));
  }
  private restore(): void {
    try {
      const storage = this.storage(); const raw = storage?.getItem(sessionKey); if (!raw) return;
      const saved = JSON.parse(raw); const claims = typeof saved.token === 'string' ? this.claims(saved.token) : {};
      const expiry = Math.min(saved.expiresAt, typeof claims.exp === 'number' ? claims.exp * 1000 : Infinity);
      if (typeof saved.token !== 'string' || !saved.token || !Number.isFinite(expiry) || expiry <= Date.now() || expiry > Date.now() + 1_800_000) {
        storage?.removeItem(sessionKey); return;
      }
      this.setSession(saved.token, expiry);
    } catch { try { this.storage()?.removeItem(sessionKey); } catch { /* Storage may be unavailable. */ } }
  }

  login(request: LoginRequest) {
    const attempt = ++this.revision;
    return this.http.post<TokenResponse>(endpoint(this.config.apiBaseUrl, '/auth/login'), {
      email: request.email.trim(), password: request.password,
    }).pipe(tap(response => {
      if (attempt !== this.revision) throw new Error('Sign-in attempt no longer active.');
      if (!response.accessToken || response.tokenType !== 'Bearer' || response.expiresIn !== 1800) throw new Error('Unexpected authentication response.');
      const claims = this.claims(response.accessToken);
      const expiry = Math.min(Date.now() + response.expiresIn * 1000, typeof claims.exp === 'number' ? claims.exp * 1000 : Infinity);
      if (!Number.isFinite(expiry) || expiry <= Date.now()) throw new Error('Expired authentication response.');
      this.setSession(response.accessToken, expiry);
      try { this.storage()?.setItem(sessionKey, JSON.stringify({ token: response.accessToken, expiresAt: expiry })); } catch { /* Sign-in remains usable until refresh. */ }
      this.sessionMessage.set('');
      this.accessDeniedMessage.set('');
    }));
  }
  accessToken(): string | null {
    if (this.token() && Date.now() >= this.expiresAt()) { this.logout(true); return null; }
    return this.token();
  }
  logout(expired = false): void {
    ++this.revision;
    clearTimeout(this.expiryTimer);
    try { this.storage()?.removeItem(sessionKey); } catch { /* Storage may be unavailable. */ }
    this.token.set(null); this.expiresAt.set(0); this.role.set(null); this.actorId.set(null);
    this.accessDeniedMessage.set('');
    this.sessionMessage.set(expired ? 'Your session has ended. Please sign in again.' : 'You have signed out.');
    const current = this.router.url;
    const returnUrl = expired ? safeAdminReturnUrl(current) : '/admin';
    void this.router.navigate(['/admin/login'], { queryParams: returnUrl === '/admin' ? {} : { returnUrl } });
  }
}
