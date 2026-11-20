import { inject } from '@angular/core';
import { CanActivateChildFn, CanActivateFn, Router } from '@angular/router';
import { AuthService, safeAdminReturnUrl } from './auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  return inject(AuthService).accessToken() ? true : inject(Router).createUrlTree(['/admin/login'], {
    queryParams: { returnUrl: safeAdminReturnUrl(state.url) },
  });
};
export const adminChildGuard: CanActivateChildFn = (route, state) => authGuard(route, state);
export const organisationAdminGuard: CanActivateFn = () =>
  inject(AuthService).role() === 'TenantAdmin' ? true : inject(Router).createUrlTree(['/admin']);
