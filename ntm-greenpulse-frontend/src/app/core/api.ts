import { Injectable, inject } from "@angular/core";
import { HttpClient, HttpParams } from "@angular/common/http";
import { CONFIG } from "./config";
import {
  Administrator,
  Business,
  BusinessWrite,
  Dashboard,
  Page,
  Query,
  Veteran,
  VeteranWrite,
} from "./models";

export const VETERAN_FIELDS = [
  "fullName",
  "mkmvaNumber",
  "dmvNumber",
  "contactNumber",
  "emailAddress",
  "membershipReference",
] as const satisfies readonly (keyof VeteranWrite)[];
export const BUSINESS_FIELDS = [
  "veteranId",
  "businessName",
  "cipcRegistrationNumber",
  "businessAddress",
  "sector",
  "businessBackground",
  "operationalStatus",
  "numberOfEmployees",
  "yearsInOperation",
  "currentBusinessNeeds",
  "priorityNeed",
  "fundingRequired",
  "fundingAmount",
  "additionalComments",
  "assessmentDate",
  "assessedBy",
] as const satisfies readonly (keyof BusinessWrite)[];
export function pick<T extends object>(
  value: T,
  keys: readonly (keyof T)[],
): T {
  return Object.fromEntries(
    keys
      .filter((key) => value[key] !== undefined)
      .map((key) => [key, value[key]]),
  ) as T;
}
function params(query: Query, keys: readonly (keyof Query)[]): HttpParams {
  let result = new HttpParams()
    .set("page", query.page ?? 1)
    .set("pageSize", query.pageSize ?? 20);
  for (const key of keys) {
    const value = query[key];
    if (
      value !== undefined &&
      (typeof value !== "string" || value.trim() !== "")
    )
      result = result.set(
        key,
        typeof value === "string" ? value.trim() : value,
      );
  }
  return result;
}
@Injectable({ providedIn: "root" })
export class Api {
  private readonly http = inject(HttpClient);
  readonly config = inject(CONFIG);
  private readonly root = `${this.config.apiBaseUrl}/admin/tenants/${encodeURIComponent(this.config.tenantCode)}/ntm`;
  private readonly admins = `${this.config.apiBaseUrl}/admin-users`;
  private adminParams(query: Query = {}) {
    return params(query, ["search", "isActive"]).set(
      "tenantCode",
      this.config.tenantCode,
    );
  }
  dashboard() {
    return this.http.get<Dashboard>(`${this.root}/dashboard`);
  }
  veterans(query: Query = {}) {
    return this.http.get<Page<Veteran>>(`${this.root}/veterans`, {
      params: params(query, ["search", "hasBusiness"]),
    });
  }
  veteran(id: string) {
    return this.http.get<Veteran>(
      `${this.root}/veterans/${encodeURIComponent(id)}`,
    );
  }
  createVeteran(value: VeteranWrite) {
    return this.http.post<Veteran>(
      `${this.root}/veterans`,
      pick(value, VETERAN_FIELDS),
    );
  }
  updateVeteran(id: string, value: VeteranWrite) {
    return this.http.put<Veteran>(
      `${this.root}/veterans/${encodeURIComponent(id)}`,
      pick(value, VETERAN_FIELDS),
    );
  }
  businesses(query: Query = {}) {
    return this.http.get<Page<Business>>(`${this.root}/businesses`, {
      params: params(query, [
        "search",
        "sector",
        "operationalStatus",
        "veteranId",
      ]),
    });
  }
  business(id: string) {
    return this.http.get<Business>(
      `${this.root}/businesses/${encodeURIComponent(id)}`,
    );
  }
  createBusiness(value: BusinessWrite) {
    return this.http.post<Business>(
      `${this.root}/businesses`,
      pick(value, BUSINESS_FIELDS),
    );
  }
  updateBusiness(id: string, value: BusinessWrite) {
    return this.http.put<Business>(
      `${this.root}/businesses/${encodeURIComponent(id)}`,
      pick(value, BUSINESS_FIELDS),
    );
  }
  administrators(query: Query = {}) {
    return this.http.get<Page<Administrator>>(this.admins, {
      params: this.adminParams(query),
    });
  }
  createAdministrator(email: string, password: string, role = "TenantStaff") {
    return this.http.post<Administrator>(
      this.admins,
      { email, password, role },
      { params: this.adminParams() },
    );
  }
  updateAdministrator(id: string, email: string, role = "TenantStaff") {
    return this.http.put<Administrator>(
      `${this.admins}/${encodeURIComponent(id)}`,
      { email, role },
      { params: this.adminParams() },
    );
  }
  administratorStatus(id: string, isActive: boolean) {
    return this.http.patch<Administrator>(
      `${this.admins}/${encodeURIComponent(id)}/status`,
      { isActive },
      { params: this.adminParams() },
    );
  }
  resetPassword(id: string, newPassword: string) {
    return this.http.post<void>(
      `${this.admins}/${encodeURIComponent(id)}/reset-password`,
      { newPassword },
      { params: this.adminParams() },
    );
  }
  changeOwnPassword(currentPassword: string, newPassword: string, confirmPassword: string) {
    return this.http.post<void>(`${this.config.apiBaseUrl}/auth/change-password`, { currentPassword, newPassword, confirmPassword });
  }
}
