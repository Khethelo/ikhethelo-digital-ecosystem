import { DestroyRef, Injectable, inject, signal } from "@angular/core";
import {
  HttpClient,
  HttpErrorResponse,
  HttpInterceptorFn,
} from "@angular/common/http";
import { CanActivateFn, Router } from "@angular/router";
import { catchError, tap, throwError } from "rxjs";
import { CONFIG } from "./config";

const sessionKey = "ntm-greenpulse-session";

export function safeReturn(value: string | null): string {
  return value &&
    /^\/(dashboard|veterans|businesses|administrators)(?:[/?#]|$)/.test(
      value,
    ) &&
    !/[\\\r\n]/.test(value)
    ? value
    : "/dashboard";
}
@Injectable({ providedIn: "root" })
export class Auth {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly config = inject(CONFIG);
  private access: string | null = null;
  private expiresAt = 0;
  private timer?: ReturnType<typeof setTimeout>;
  private revision = 0;
  readonly signedIn = signal(false);
  readonly message = signal("");
  readonly actorId = signal<string | null>(null);
  constructor() {
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
    try {
      const saved = JSON.parse(sessionStorage.getItem(sessionKey) ?? "null");
      if (typeof saved?.access === "string" && saved.access &&
          Number.isFinite(saved.expiresAt) && saved.expiresAt > Date.now() &&
          saved.expiresAt <= Date.now() + 1800000) {
        this.setSession(saved.access, saved.expiresAt);
      } else {
        sessionStorage.removeItem(sessionKey);
      }
    } catch {
      try { sessionStorage.removeItem(sessionKey); } catch { /* Storage is unavailable. */ }
    }
  }
  private setSession(access: string, expiresAt: number) {
    this.access = access;
    this.expiresAt = expiresAt;
    this.signedIn.set(true);
    this.message.set("");
    clearTimeout(this.timer);
    try {
      const payload = access.split(".")[1].replace(/-/g, "+").replace(/_/g, "/");
      this.actorId.set(JSON.parse(atob(payload)).sub ?? null);
    } catch { this.actorId.set(null); }
    this.timer = setTimeout(() => this.logout(true), Math.max(0, expiresAt - Date.now()));
  }
  login(email: string, password: string) {
    const revision = ++this.revision;
    return this.http
      .post<{ accessToken: string; tokenType: string; expiresIn: number }>(
        `${this.config.apiBaseUrl}/auth/login`,
        { email: email.trim(), password },
      )
      .pipe(
        tap((response) => {
          if (
            revision !== this.revision ||
            !response.accessToken ||
            response.tokenType !== "Bearer" ||
            response.expiresIn !== 1800
          )
            throw new Error("Unexpected authentication response.");
          const expiresAt = Date.now() + response.expiresIn * 1000;
          this.setSession(response.accessToken, expiresAt);
          try { sessionStorage.setItem(sessionKey, JSON.stringify({ access: response.accessToken, expiresAt })); } catch { /* Continue in memory if storage is unavailable. */ }
        }),
      );
  }
  token(): string | null {
    if (this.access && Date.now() >= this.expiresAt) {
      this.logout(true);
      return null;
    }
    return this.access;
  }
  clear() {
    ++this.revision;
    this.access = null;
    this.expiresAt = 0;
    this.signedIn.set(false);
    this.actorId.set(null);
    clearTimeout(this.timer);
    try { sessionStorage.removeItem(sessionKey); } catch { /* Storage is unavailable. */ }
  }
  logout(expired = false, notice?: string) {
    const returnUrl = safeReturn(this.router.url);
    this.clear();
    this.message.set(
      notice ?? (expired
        ? "Your session has ended. Sign in again to continue."
        : "You have signed out."),
    );
    void this.router.navigate(["/login"], {
      queryParams: expired ? { returnUrl } : {},
    });
  }
}
export const authGuard: CanActivateFn = (_route, state) =>
  inject(Auth).token()
    ? true
    : inject(Router).createUrlTree(["/login"], {
        queryParams: { returnUrl: safeReturn(state.url) },
      });
export const sessionInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(Auth);
  const base = inject(CONFIG).apiBaseUrl;
  const protectedApi =
    request.url === `${base}/auth/change-password` ||
    request.url.startsWith(`${base}/admin/`) ||
    request.url === `${base}/admin-users` ||
    request.url.startsWith(`${base}/admin-users/`);
  const token = protectedApi ? auth.token() : null;
  return next(
    token
      ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : request,
  ).pipe(
    catchError((error: unknown) => {
      if (
        protectedApi &&
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        auth.token() === token
      )
        auth.logout(true);
      return throwError(() => error);
    }),
  );
};
