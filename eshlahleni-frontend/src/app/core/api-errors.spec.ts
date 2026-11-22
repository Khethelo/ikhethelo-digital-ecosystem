import { describe, expect, it } from 'vitest';
import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { FormControl, FormGroup } from '@angular/forms';
import { apiIssue, applyFieldErrors } from './api-errors';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ApiError } from '../shared/ui';
describe('safe actionable API errors', () => {
  it('handles unavailable, forbidden, missing, conflicting and unexpected failures without raw server details', () => {
    for (const status of [0, 401, 403, 404, 409, 500]) {
      const issue = apiIssue(new HttpErrorResponse({ status, error: { title: 'internal-secret-do-not-render' } }));
      expect(issue.status).toBe(status); expect(issue.message).not.toContain('internal-secret'); expect(issue.message.length).toBeGreaterThan(10);
    }
  });
  it('maps known 422 fields and preserves Retry-After for 429', () => {
    const form = new FormGroup({ email: new FormControl('wrong@example.test') });
    applyFieldErrors(form, apiIssue(new HttpErrorResponse({ status: 422, error: { errors: { email: ['Choose another email'], nonexistent: ['unknown'] } } })));
    expect(form.controls.email.errors?.['server']).toEqual(['Enter a valid email address.']); expect(form.controls.email.touched).toBe(true);
    expect(apiIssue(new HttpErrorResponse({ status: 429, headers: new HttpHeaders({ 'Retry-After': '60' }) })).retryAfter).toBe(60);
  });
  it('uses safe messages and never displays arbitrary error fields', () => {
    expect(apiIssue(new Error('secret stack')).message).toContain('unexpected');
    expect(apiIssue(new HttpErrorResponse({ status: 503 })).message).toContain('temporarily unavailable');
    expect(apiIssue(new HttpErrorResponse({ status: 429 }), 'login').message).toContain('sign-in');
    const issue = apiIssue(new HttpErrorResponse({ status: 422, error: { errors: { email: ['secret connection string'], unknown: ['SQL details'] } } }));
    expect(JSON.stringify(issue)).not.toContain('secret'); expect(issue.fields['email']).toEqual(['Enter a valid email address.']);
  });
  it('only accepts a safe application ID with the expected protected conflict', () => {
    const title = 'A pending membership application already exists for this email address or phone number. Review the existing application instead.';
    const id = '7f111111-1111-4111-8111-111111111111';
    expect(apiIssue(new HttpErrorResponse({ status: 409, error: { title, existingApplicationId: id } }), 'registration').existingApplicationId).toBe(id);
    expect(apiIssue(new HttpErrorResponse({ status: 409, error: { title, existingApplicationId: '../users' } }), 'registration').existingApplicationId).toBeUndefined();
    expect(apiIssue(new HttpErrorResponse({ status: 409, error: { title, existingApplicationId: id } }), 'application').existingApplicationId).toBeUndefined();
  });
  it('renders a direct review link for a validated pending conflict', async () => {
    TestBed.configureTestingModule({ imports: [ApiError], providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(ApiError);
    fixture.componentInstance.issue = { status: 409, message: 'Pending application', fields: {}, existingApplicationId: '7f111111-1111-4111-8111-111111111111' };
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    const link = fixture.nativeElement.querySelector('a') as HTMLAnchorElement;
    expect(link.textContent).toContain('Review Existing Application');
    expect(link.getAttribute('href')).toBe('/admin/applications/7f111111-1111-4111-8111-111111111111');
  });
  it('renders a member link only for a validated registration conflict', async () => {
    const id = '7f222222-2222-4222-8222-222222222222';
    const title = 'An active member already exists with this email address or phone number.';
    const issue = apiIssue(new HttpErrorResponse({ status: 409, error: { title, existingMemberId: id } }), 'registration');
    expect(issue.existingMemberId).toBe(id);
    expect(apiIssue(new HttpErrorResponse({ status: 409, error: { title, existingMemberId: '../users' } }), 'registration').existingMemberId).toBeUndefined();
    TestBed.configureTestingModule({ imports: [ApiError], providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(ApiError); fixture.componentInstance.issue = issue;
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('a').getAttribute('href')).toBe('/admin/members/' + id);
  });
});
