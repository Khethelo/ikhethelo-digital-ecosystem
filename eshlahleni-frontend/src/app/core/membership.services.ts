import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_CONFIG, endpoint } from './api.config';
import { ApplicationSubmission, ContactDetails, ListQuery, Member, MemberStatus, MembershipApplication, Page, ReviewRequest, SubmissionReceipt } from './api.models';

function parameters(query: ListQuery): HttpParams {
  let params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);
  if (query.search?.trim()) params = params.set('search', query.search.trim());
  if (query.status) params = params.set('status', query.status);
  return params;
}
@Injectable({ providedIn: 'root' })
export class PublicMembershipService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(API_CONFIG);
  submit(request: ApplicationSubmission) {
    // Construct the exact public DTO. Form-only state can never become ownership/status input.
    const body: ApplicationSubmission = {
      firstName: request.firstName.trim(), lastName: request.lastName.trim(),
      dateOfBirth: request.dateOfBirth, phoneNumber: request.phoneNumber, email: request.email.trim(),
      termsAccepted: request.termsAccepted, privacyConsent: request.privacyConsent,
    };
    return this.http.post<SubmissionReceipt>(endpoint(this.config.apiBaseUrl,
      `/public/${encodeURIComponent(this.config.publicTenantAlias)}/membership-applications`), body);
  }
}
@Injectable({ providedIn: 'root' })
export class AdminApplicationsService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(API_CONFIG);
  private readonly root = endpoint(this.config.apiBaseUrl, `/admin/tenants/${encodeURIComponent(this.config.adminTenantCode)}/membership-applications`);
  list(query: ListQuery) { return this.http.get<Page<MembershipApplication>>(this.root, { params: parameters(query) }); }
  get(id: string) { return this.http.get<MembershipApplication>(`${this.root}/${encodeURIComponent(id)}`); }
  activate(id: string, request: ReviewRequest = {}) { return this.http.post<Member>(`${this.root}/${encodeURIComponent(id)}/activate`, this.reviewBody(request)); }
  approve(id: string, request: ReviewRequest = {}) { return this.http.post<{ member: Member; confirmationEmailSent: boolean }>(`${this.root}/${encodeURIComponent(id)}/approve`, this.reviewBody(request)); }
  decline(id: string, request: ReviewRequest = {}) { return this.http.post<MembershipApplication>(`${this.root}/${encodeURIComponent(id)}/decline`, this.reviewBody(request)); }
  private reviewBody(request: ReviewRequest): ReviewRequest {
    return request.reviewNotes?.trim() ? { reviewNotes: request.reviewNotes.trim() } : {};
  }
}
@Injectable({ providedIn: 'root' })
export class AdminMembersService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(API_CONFIG);
  private readonly root = endpoint(this.config.apiBaseUrl, `/admin/tenants/${encodeURIComponent(this.config.adminTenantCode)}/members`);
  list(query: ListQuery) { return this.http.get<Page<Member>>(this.root, { params: parameters(query) }); }
  get(id: string) { return this.http.get<Member>(`${this.root}/${encodeURIComponent(id)}`); }
  update(id: string, request: ContactDetails) {
    return this.http.put<Member>(`${this.root}/${encodeURIComponent(id)}`, {
      firstName: request.firstName.trim(), lastName: request.lastName.trim(),
      phoneNumber: request.phoneNumber, email: request.email.trim(),
    } satisfies ContactDetails);
  }
  updateStatus(id: string, status: MemberStatus) { return this.http.put<Member>(`${this.root}/${encodeURIComponent(id)}/status`, { status }); }
}
