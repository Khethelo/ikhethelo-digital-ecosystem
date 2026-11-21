import { HttpErrorResponse } from '@angular/common/http';
import { FormGroup } from '@angular/forms';
import { ProblemDetails } from './api.models';

export interface ApiIssue { status: number; message: string; traceId?: string; retryAfter?: number; fields: Record<string, string[]>; }
export function apiIssue(error: unknown, context: 'application' | 'login' | 'review' | 'member' | 'registration' | 'read' = 'read'): ApiIssue {
  const response = error instanceof HttpErrorResponse ? error : null;
  const status = response?.status ?? 0;
  const problem: ProblemDetails = response?.error && typeof response.error === 'object' ? response.error : {};
  const messages: Record<number, string> = {
    0: 'We could not reach the service. Please check your connection and try again.',
    400: 'The request could not be processed. Check your details and try again.',
    401: context === 'login' ? 'The email or password is incorrect, or the account is inactive.' : 'Your session has ended. Please sign in again.',
    403: 'Your account does not have permission to access this information.',
    404: 'This record could not be found. It may no longer be available to your account.',
    409: context === 'application' ? 'A registration may already exist for these contact details. Please check with the club before applying again.'
      : context === 'review' ? 'This registration has changed or has already been completed. Reload its details before continuing.'
      : context === 'registration' ? (['A pending membership application already exists for this email address or phone number. Review the existing application instead.', 'An active member already exists with this email address or phone number.'].includes(problem.title ?? '') ? problem.title! : 'A pending application or active member may already exist for these contact details. Check applications and members before trying again.')
      : 'These details conflict with another active record or the current state. Please reload and check the details.',
    413: 'The submission is too large. Please shorten the information and try again.',
    422: 'Please check the highlighted details and try again.',
    429: 'Too many attempts. Please wait before trying again.',
  };
  const fields: Record<string, string[]> = Object.create(null);
  if (problem.errors && typeof problem.errors === 'object') {
    for (const [key, values] of Object.entries(problem.errors)) {
      if (Array.isArray(values)) fields[key] = values.filter((value): value is string => typeof value === 'string').map(value => value.slice(0, 500));
    }
  }
  const delay = Number(response?.headers.get('Retry-After'));
  return {
    status, message: messages[status] ?? 'Something went wrong. Please try again later.', fields,
    traceId: typeof problem.traceId === 'string' ? problem.traceId.slice(0, 150) : undefined,
    retryAfter: status === 429 && Number.isFinite(delay) && delay > 0 ? Math.ceil(delay) : undefined,
  };
}
export function applyFieldErrors(form: FormGroup, issue: ApiIssue): void {
  for (const [name, errors] of Object.entries(issue.fields)) {
    const control = form.get(name);
    if (control && errors.length) { control.setErrors({ ...control.errors, server: errors }); control.markAsTouched(); }
  }
}
