import { HttpErrorResponse } from '@angular/common/http';
import { FormGroup } from '@angular/forms';
import { ProblemDetails } from './api.models';

export interface ApiIssue { status: number; message: string; traceId?: string; retryAfter?: number; existingApplicationId?: string; existingMemberId?: string; fields: Record<string, string[]>; }
export function apiIssue(error: unknown, context: 'application' | 'login' | 'review' | 'member' | 'registration' | 'users' | 'password' | 'read' = 'read'): ApiIssue {
  const response = error instanceof HttpErrorResponse ? error : null;
  const status = response?.status ?? 0;
  const problem: ProblemDetails = response?.error && typeof response.error === 'object' ? response.error : {};
  const messages: Record<number, string> = {
    0: response ? 'We could not reach the service. Please check your connection and try again.' : 'Something unexpected happened. Please refresh the page and try again.',
    400: 'The request could not be processed. Check your details and try again.',
    401: context === 'login' ? 'The email or password is incorrect, or the account is inactive.' : 'Your session has ended. Please sign in again.',
    403: context === 'users' && problem.title === 'Use change password for your own account.' ? 'Use My Account to change your own password.' : 'Your account does not have permission to access this information.',
    404: 'This record could not be found. It may no longer be available to your account.',
    409: context === 'application' ? 'A registration may already exist for these contact details. Please check with the club before applying again.'
      : context === 'review' ? 'This registration has changed or has already been completed. Reload its details before continuing.'
      : context === 'users' && ['An administrator already uses this email address.', 'You cannot deactivate or demote the last active Organisation Administrator.', 'You cannot deactivate your own account.'].includes(problem.title ?? '') ? problem.title!
      : context === 'registration' ? (['A pending membership application already exists for this email address or phone number. Review the existing application instead.', 'An active member already exists with this email address or phone number.'].includes(problem.title ?? '') ? problem.title! : 'A pending application or active member may already exist for these contact details. Check applications and members before trying again.')
      : 'These details conflict with another active record or the current state. Please reload and check the details.',
    413: 'The submission is too large. Please shorten the information and try again.',
    415: 'The request could not be processed. Please refresh the page and try again.',
    422: 'Please check the highlighted details and try again.',
    429: context === 'login' ? 'Too many sign-in attempts. Please wait before trying again.' : 'Too many attempts. Please wait before trying again.',
    500: context === 'login' ? 'Something went wrong while signing you in. Please try again.' : context === 'password' ? 'Your password could not be changed right now. Please try again.' : 'Something went wrong on our side. Please try again.',
    502: 'The service is temporarily unavailable. Please try again shortly.',
    503: 'The service is temporarily unavailable. Please try again shortly.',
    504: 'The service is temporarily unavailable. Please try again shortly.',
  };
  const fields: Record<string, string[]> = Object.create(null);
  const safeFields: Record<string, string> = {
    firstName: 'Use 2 to 100 characters.', lastName: 'Use 2 to 100 characters.',
    email: 'Enter a valid email address.', phoneNumber: 'Enter a valid phone number, for example 0843190994.',
    dateOfBirth: 'Enter a valid date of birth. Members must be at least 18 years old.',
    termsAccepted: 'Membership terms must be accepted.', privacyConsent: 'Privacy consent is required.',
    confirmPassword: 'Passwords do not match.', currentPassword: 'Current password is incorrect.',
    reviewNotes: 'Use 1 to 2000 characters.', status: 'Choose a valid status.', role: 'Choose a valid role.',
    password: 'Use 8 to 128 characters, including an ASCII digit and a special character (not a letter, number, or whitespace).', request: 'Please check your details and try again.',
  };
  if (status === 422 && problem.errors && typeof problem.errors === 'object') {
    for (const [key, values] of Object.entries(problem.errors)) {
      if (Array.isArray(values) && values.length && safeFields[key]) {
        fields[key] = [key === 'password' && values.includes('Choose a password different from your current password.')
          ? 'Choose a password different from your current password.' : safeFields[key]];
      }
    }
  }
  const delay = Number(response?.headers.get('Retry-After'));
  return {
    status, message: messages[status] ?? 'Something went wrong. Please try again later.', fields,
    traceId: typeof problem.traceId === 'string' && /^[A-Za-z0-9:._-]{1,150}$/.test(problem.traceId) ? problem.traceId : undefined,
    retryAfter: status === 429 && Number.isFinite(delay) && delay > 0 ? Math.ceil(delay) : undefined,
    existingApplicationId: context === 'registration' && status === 409 && problem.title === 'A pending membership application already exists for this email address or phone number. Review the existing application instead.' &&
      typeof problem.existingApplicationId === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(problem.existingApplicationId) ? problem.existingApplicationId : undefined,
    existingMemberId: context === 'registration' && status === 409 && problem.title === 'An active member already exists with this email address or phone number.' &&
      typeof problem.existingMemberId === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(problem.existingMemberId) ? problem.existingMemberId : undefined,
  };
}
export function applyFieldErrors(form: FormGroup, issue: ApiIssue): void {
  for (const [name, errors] of Object.entries(issue.fields)) {
    const control = form.get(name);
    if (control && errors.length) { control.setErrors({ ...control.errors, server: errors }); control.markAsTouched(); }
  }
}
