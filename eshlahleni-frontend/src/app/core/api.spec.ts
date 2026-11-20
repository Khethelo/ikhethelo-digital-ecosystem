import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { AdminApplicationsService, AdminMembersService, PublicMembershipService } from './membership.services';
import { AuthService, safeAdminReturnUrl } from './auth.service';
import { authInterceptor } from './auth.interceptor';

describe('contract and administrator session', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { http.verify(); vi.useRealTimers(); });
  function login() {
    TestBed.inject(AuthService).login({ email: ' admin@example.test ', password: 'temporary-password' }).subscribe();
    const request = http.expectOne('/api/v1/auth/login');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({ accessToken: 'test-token', tokenType: 'Bearer', expiresIn: 1800 });
  }
  it('whitelists public fields and preserves the calendar birth date', () => {
    TestBed.inject(PublicMembershipService).submit({ firstName: ' John ', lastName: ' Gotti ', dateOfBirth: '2000-11-05', phoneNumber: '+27843517720', email: ' john@example.test ', termsAccepted: true, privacyConsent: true, categoryId: 'not-allowed' } as any).subscribe();
    const request = http.expectOne('/api/v1/public/eshlahleni/membership-applications');
    expect(Object.keys(request.request.body).sort()).toEqual(['dateOfBirth','email','firstName','lastName','phoneNumber','privacyConsent','termsAccepted'].sort());
    expect(request.request.body.dateOfBirth).toBe('2000-11-05');
    expect(request.request.body.firstName).toBe('John');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({ applicationId: 'id', status: 'AwaitingInPersonCompletion', submittedAtUtc: '2026-11-06T00:00:00Z', message: 'Received' });
  });
  it('uses server filters and exact review and member status payloads', () => {
    const apps = TestBed.inject(AdminApplicationsService);
    apps.list({ page: 2, pageSize: 20, status: 'AwaitingInPersonCompletion', search: ' Ada ' }).subscribe();
    const list = http.expectOne(request => request.url.endsWith('/membership-applications'));
    expect(list.request.params.get('page')).toBe('2'); expect(list.request.params.get('search')).toBe('Ada'); list.flush({ items: [], totalCount: 0, page: 2, pageSize: 20 });
    apps.activate('abc', { reviewNotes: '  Checked  ' }).subscribe();
    const approve = http.expectOne('/api/v1/admin/tenants/ESHL/membership-applications/abc/activate'); expect(approve.request.body).toEqual({ reviewNotes: 'Checked' }); approve.flush({});
    TestBed.inject(AdminMembersService).updateStatus('member', 'Suspended').subscribe(); const status = http.expectOne('/api/v1/admin/tenants/ESHL/members/member/status'); expect(status.request.method).toBe('PUT'); expect(status.request.body).toEqual({ status: 'Suspended' }); status.flush({});
  });
  it('sends the in-memory token only to the configured admin API', () => {
    login(); const client = TestBed.inject(HttpClient);
    for (const url of ['/api/v1/admin/tenants/ESHL/members', '/api/v1/public/eshlahleni/membership-applications', 'https://elsewhere.example/api/v1/admin/users']) {
      client.get(url).subscribe(); const request = http.expectOne(url);
      expect(request.request.headers.get('Authorization')).toBe(url.startsWith('/api/v1/admin/') ? 'Bearer test-token' : null); request.flush({});
    }
    expect(localStorage.length).toBe(0); expect(sessionStorage.length).toBe(0);
  });
  it('expires at fifteen minutes and clears the token on a protected 401', () => {
    vi.useFakeTimers(); const auth = TestBed.inject(AuthService); vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    login(); vi.advanceTimersByTime(900_000); expect(auth.accessToken()).toBeNull();
    login(); TestBed.inject(HttpClient).get('/api/v1/admin/tenants/ESHL/members').subscribe({ error: () => {} }); http.expectOne('/api/v1/admin/tenants/ESHL/members').flush({}, { status: 401, statusText: 'Unauthorized' }); expect(auth.accessToken()).toBeNull();
  });
  it('rejects external and login redirect destinations', () => {
    expect(safeAdminReturnUrl('//evil.example')).toBe('/admin');
    expect(safeAdminReturnUrl('/admin/login')).toBe('/admin');
    expect(safeAdminReturnUrl('/admin/members?page=2')).toBe('/admin/members?page=2');
  });
});
