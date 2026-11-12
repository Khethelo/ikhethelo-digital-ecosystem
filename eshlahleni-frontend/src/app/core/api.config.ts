import { InjectionToken } from '@angular/core';
import { environment } from '../../environments/environment';

export interface ApiConfig { apiBaseUrl: string; publicTenantAlias: string; adminTenantCode: string; }
export const API_CONFIG = new InjectionToken<ApiConfig>('ESHL_API_CONFIG', {
  providedIn: 'root', factory: () => environment,
});
export function endpoint(base: string, path: string): string { return base.replace(/\/$/, '') + path; }
