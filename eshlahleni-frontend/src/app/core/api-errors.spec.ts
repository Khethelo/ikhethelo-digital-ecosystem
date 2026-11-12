import { describe, expect, it } from 'vitest';
import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { FormControl, FormGroup } from '@angular/forms';
import { apiIssue, applyFieldErrors } from './api-errors';
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
    expect(form.controls.email.errors?.['server']).toEqual(['Choose another email']); expect(form.controls.email.touched).toBe(true);
    expect(apiIssue(new HttpErrorResponse({ status: 429, headers: new HttpHeaders({ 'Retry-After': '60' }) })).retryAfter).toBe(60);
  });
});
