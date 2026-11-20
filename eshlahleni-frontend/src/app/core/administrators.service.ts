import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_CONFIG, endpoint } from './api.config';
import { Page } from './api.models';

export interface Administrator { id: string; email: string; role: 'TenantAdmin' | 'TenantStaff'; isActive: boolean; }
@Injectable({ providedIn: 'root' })
export class AdministratorsService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(API_CONFIG);
  private readonly root = endpoint(this.config.apiBaseUrl, '/admin-users');
  private readonly params = new HttpParams().set('tenantCode', this.config.adminTenantCode);
  list(page = 1) { return this.http.get<Page<Administrator>>(this.root, { params: this.params.set('page', page).set('pageSize', 100) }); }
  create(email: string, password: string, role: string) { return this.http.post<Administrator>(this.root, { email: email.trim(), password, role }, { params: this.params }); }
  update(id: string, email: string, role: string) { return this.http.put<Administrator>(`${this.root}/${encodeURIComponent(id)}`, { email: email.trim(), role }, { params: this.params }); }
  status(id: string, isActive: boolean) { return this.http.patch<Administrator>(`${this.root}/${encodeURIComponent(id)}/status`, { isActive }, { params: this.params }); }
  reset(id: string, newPassword: string) { return this.http.post<void>(`${this.root}/${encodeURIComponent(id)}/reset-password`, { newPassword }, { params: this.params }); }
  passwordPolicy() { return this.http.get<{ guidance: string }>(endpoint(this.config.apiBaseUrl, '/auth/password-policy')); }
  changeOwn(currentPassword: string, newPassword: string, confirmPassword: string) {
    return this.http.post<void>(endpoint(this.config.apiBaseUrl, '/auth/change-password'), { currentPassword, newPassword, confirmPassword });
  }
}
