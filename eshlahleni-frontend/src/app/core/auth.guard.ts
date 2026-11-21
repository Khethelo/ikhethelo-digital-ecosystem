import { inject } from '@angular/core';
import { CanActivateChildFn, CanActivateFn, Router } from '@angular/router';
import { AuthService, safeAdminReturnUrl } from './auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  return inject(AuthService).accessToken() ? true : inject(Router).createUrlTree(['/admin/login'], {
    queryParams: { returnUrl: safeAdminReturnUrl(state.url) },
  });
};
export const adminChildGuard: CanActivateChildFn = (route, state) => authGuard(route, state);
export const organisationAdminGuard: CanActivateFn = () => {
  const auth = inject(AuthService); const router = inject(Router);
  if (auth.role() === 'TenantAdmin') return true;
  auth.accessDeniedMessage.set('You don’t have permission to access User Management.');
  return router.createUrlTree(['/admin']);
};
