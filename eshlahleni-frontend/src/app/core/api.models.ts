/** Explicit client DTOs aligned with contracts/ikhethelo-digital-api.openapi.yaml v1.1.0. */
export type ApplicationStatus = 'AwaitingInPersonCompletion' | 'Completed' | 'LegacyClosed';
export type MemberStatus = 'Active' | 'Suspended' | 'Inactive';
export interface ContactDetails {
  firstName: string;
  lastName: string;
  phoneNumber: string;
  email: string;
}
export interface ApplicationSubmission extends ContactDetails {
  dateOfBirth: string;
  termsAccepted: boolean;
  privacyConsent: boolean;
}
export interface SubmissionReceipt {
  applicationId: string;
  status: 'AwaitingInPersonCompletion';
  submittedAtUtc: string;
  message: string;
}
export interface MembershipApplication extends ApplicationSubmission {
  applicationId: string;
  status: ApplicationStatus;
  submittedAtUtc: string;
}
export interface Member extends ContactDetails {
  memberId: string;
  applicationId: string;
  memberNumber: string;
  dateOfBirth: string;
  status: MemberStatus;
  joinedAtUtc: string;
}
export interface Page<T> { items: T[]; page: number; pageSize: number; totalCount: number; }
export interface ListQuery { page: number; pageSize: number; search?: string; status?: string; }
export interface ReviewRequest { reviewNotes?: string; }
export interface LoginRequest { email: string; password: string; }
export interface TokenResponse { accessToken: string; tokenType: 'Bearer'; expiresIn: number; }
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  traceId?: string;
  errors?: Record<string, string[]>;
}
export const applicationStatuses: ApplicationStatus[] = ['AwaitingInPersonCompletion', 'Completed', 'LegacyClosed'];
export const memberStatuses: MemberStatus[] = ['Active', 'Suspended', 'Inactive'];
