import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { API_CONFIG } from './api.config';
import { AuthService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const base = inject(API_CONFIG).apiBaseUrl.replace(/\/$/, '');
  const protectedApi = request.url.startsWith(`${base}/admin/`);
  const token = protectedApi ? auth.accessToken() : null;
  const outgoing = token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;
  return next(outgoing).pipe(catchError((error: unknown) => {
    if (protectedApi && error instanceof HttpErrorResponse && error.status === 401 && auth.accessToken() === token) auth.logout(true);
    return throwError(() => error);
  }));
};
