import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { TestBed } from "@angular/core/testing";
import { FormBuilder, FormControl } from "@angular/forms";
import {
  HttpClient,
  provideHttpClient,
  withInterceptors,
} from "@angular/common/http";
import {
  HttpTestingController,
  provideHttpClientTesting,
} from "@angular/common/http/testing";
import { Router, provideRouter } from "@angular/router";
import { Api } from "./api";
import { Auth, safeReturn, sessionInterceptor } from "./auth";
import { CONFIG, validateConfig } from "./config";
import { adminPassword, calendarDate, numeric, registration, requiredText } from "./forms";

describe("form and configuration safeguards", () => {
  it("enforces the administrator password policy without trimming", () => {
    for (const value of ["ab1!xyz", "abcdefgh!", "abcdefgh1", "abcdefgh1 "])
      expect(new FormControl(value, adminPassword).invalid).toBe(true);
    for (const value of ["abcdef1!", "密碼密碼ab1!", "pass word1!"])
      expect(new FormControl(value, adminPassword).valid).toBe(true);
  });
  it("accepts either registration number and rejects missing identifiers", () => {
    const fb = new FormBuilder();
    const form = fb.group(
      { mkmvaNumber: [""], dmvNumber: [""] },
      { validators: registration },
    );
    expect(form.invalid).toBe(true);
    form.patchValue({ mkmvaNumber: " MK-1 " });
    expect(form.valid).toBe(true);
    form.patchValue({ mkmvaNumber: " ", dmvNumber: "DMV-1" });
    expect(form.valid).toBe(true);
    form.patchValue({ dmvNumber: "\t" });
    expect(form.invalid).toBe(true);
  });
  it("does not round invalid amounts, counts or years", () => {
    expect(
      numeric(9999999999999.99, 2)(new FormControl(12.345)),
    ).not.toBeNull();
    expect(numeric(99999.9, 1)(new FormControl(1.25))).not.toBeNull();
    expect(numeric(2147483647, 0)(new FormControl(3.5))).not.toBeNull();
    expect(numeric(100, 2)(new FormControl(-1))).not.toBeNull();
    expect(numeric(100, 2)(new FormControl(null))).toBeNull();
    expect(numeric(100, 2)(new FormControl(0))).toBeNull();
  });
  it("validates calendar dates without shifting timezones", () => {
    expect(calendarDate(new FormControl("2024-02-29"))).toBeNull();
    expect(calendarDate(new FormControl("2026-02-29"))).not.toBeNull();
    expect(calendarDate(new FormControl("2026.10.13"))).not.toBeNull();
    expect(calendarDate(new FormControl(null))).toBeNull();
  });
  it("rejects whitespace-only names", () =>
    expect(requiredText(new FormControl(" "))).not.toBeNull());
  it("allows only same-origin API configuration and canonical tenant codes", () => {
    expect(
      validateConfig({ apiBaseUrl: "/api/v1", tenantCode: "NTM" }).tenantCode,
    ).toBe("NTM");
    for (const apiBaseUrl of [
      "https://elsewhere.test/api",
      "//elsewhere.test/api",
      "/api/../other",
    ])
      expect(() => validateConfig({ apiBaseUrl, tenantCode: "NTM" })).toThrow();
    expect(() =>
      validateConfig({ apiBaseUrl: "/api/v1", tenantCode: "../../other" }),
    ).toThrow();
  });
  it("rejects external and login return destinations", () => {
    for (const value of [
      "//evil.test",
      "https://evil.test",
      "/login",
      "/businesses\\evil",
      "/businesses\n",
    ])
      expect(safeReturn(value)).toBe("/dashboard");
    expect(safeReturn("/businesses/b-1")).toBe("/businesses/b-1");
  });
});

describe("authenticated NTM API", () => {
  let http: HttpTestingController;
  let api: Api;
  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([sessionInterceptor])),
        provideHttpClientTesting(),
        {
          provide: CONFIG,
          useValue: { apiBaseUrl: "/api/v1", tenantCode: "NTM" },
        },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    api = TestBed.inject(Api);
  });
  afterEach(() => {
    http.verify();
    vi.useRealTimers();
  });
  function login() {
    TestBed.inject(Auth)
      .login("admin@example.test", "not-a-real-password")
      .subscribe();
    const req = http.expectOne("/api/v1/auth/login");
    expect(req.request.headers.has("Authorization")).toBe(false);
    req.flush({
      accessToken: "test-token",
      tokenType: "Bearer",
      expiresIn: 1800,
    });
  }
  it("omits source fields and preserves null, zero, false and calendar dates", () => {
    const body = {
      veteranId: "v1",
      businessName: "Business",
      fundingRequired: false,
      fundingAmount: 0,
      numberOfEmployees: null,
      numberOfEmployeesSource: " 3 part time\n",
      yearsInOperationSource: "Needs equipment",
      operationalStatusSource: "Operationl",
      fundingRequiredSource: "Funding",
      assessmentDate: "2026-10-13",
      tenantId: "forged",
    };
    api.createBusiness(body).subscribe();
    const req = http.expectOne("/api/v1/admin/tenants/NTM/ntm/businesses");
    expect(req.request.body.numberOfEmployeesSource).toBeUndefined();
    expect(req.request.body.fundingRequired).toBe(false);
    expect(req.request.body.fundingAmount).toBe(0);
    expect(req.request.body.numberOfEmployees).toBeNull();
    expect(req.request.body.assessmentDate).toBe("2026-10-13");
    expect(req.request.body.tenantId).toBeUndefined();
    req.flush({});
  });
  it("whitelists veteran writes without trimming the original membership reference", () => {
    api
      .createVeteran({
        fullName: "Veteran",
        mkmvaNumber: "MK-1",
        membershipReference: " DMV Database ",
        tenantId: "forged",
      } as any)
      .subscribe();
    const req = http.expectOne("/api/v1/admin/tenants/NTM/ntm/veterans");
    expect(req.request.body).toEqual({
      fullName: "Veteran",
      mkmvaNumber: "MK-1",
      membershipReference: " DMV Database ",
    });
    req.flush({});
  });
  it("retains false filters and queries the server for pagination", () => {
    api
      .veterans({
        page: 2,
        pageSize: 10,
        search: " Ada ",
        hasBusiness: false,
        sector: "unused",
      })
      .subscribe();
    const req = http.expectOne((r) => r.url.endsWith("/ntm/veterans"));
    expect(req.request.params.get("hasBusiness")).toBe("false");
    expect(req.request.params.get("search")).toBe("Ada");
    expect(req.request.params.get("page")).toBe("2");
    expect(req.request.params.has("sector")).toBe(false);
    req.flush({});
  });
  it("encodes resource IDs and uses PUT for updates", () => {
    api.veteran("v/1").subscribe();
    http.expectOne("/api/v1/admin/tenants/NTM/ntm/veterans/v%2F1").flush({});
    api
      .updateBusiness("b/1", { veteranId: "v1", businessName: "Business" })
      .subscribe();
    const req = http.expectOne(
      "/api/v1/admin/tenants/NTM/ntm/businesses/b%2F1",
    );
    expect(req.request.method).toBe("PUT");
    req.flush({});
  });
  it("scopes every administrator request and prevents role escalation in payloads", () => {
    api
      .createAdministrator("other@example.test", "not-a-real-password")
      .subscribe();
    const req = http.expectOne((r) => r.url.endsWith("/admin-users"));
    expect(req.request.params.get("tenantCode")).toBe("NTM");
    expect(req.request.body.role).toBe("TenantAdmin");
    expect(req.request.body.tenantId).toBeUndefined();
    req.flush({});
    api.resetPassword("a1", "new-test-password").subscribe();
    const reset = http.expectOne((r) => r.url.endsWith("/a1/reset-password"));
    expect(reset.request.body).toEqual({ newPassword: "new-test-password" });
    expect(reset.request.params.get("tenantCode")).toBe("NTM");
    reset.flush(null);
  });
  it("sends the token only to protected same-origin API routes", () => {
    login();
    for (const url of [
      "/api/v1/admin/tenants/NTM/ntm/dashboard",
      "/api/v1/admin-users",
      "https://other.test/api/v1/admin/users",
    ]) {
      TestBed.inject(HttpClient).get(url).subscribe();
      const req = http.expectOne(url);
      expect(req.request.headers.get("Authorization")).toBe(
        url.startsWith("/api/v1/") ? "Bearer test-token" : null,
      );
      req.flush({});
    }
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.getItem("ntm-greenpulse-session")).toContain("test-token");
  });
  it("authenticates the own-password endpoint", () => {
    login();
    api.changeOwnPassword("old-password", "newpass1!", "newpass1!").subscribe();
    const req = http.expectOne("/api/v1/auth/change-password");
    expect(req.request.headers.get("Authorization")).toBe("Bearer test-token");
    req.flush(null);
  });
  it("restores a session after refresh without extending its expiry", () => {
    vi.useFakeTimers();
    login();
    vi.advanceTimersByTime(900000);
    const restored = TestBed.runInInjectionContext(() => new Auth());
    expect(restored.token()).toBe("test-token");
    vi.spyOn(TestBed.inject(Router), "navigate").mockResolvedValue(true);
    vi.advanceTimersByTime(900000);
    expect(restored.token()).toBeNull();
    expect(sessionStorage.getItem("ntm-greenpulse-session")).toBeNull();
  });
  it("clears expired sessions and handles 401 without treating 403 as logout", () => {
    vi.useFakeTimers();
    const auth = TestBed.inject(Auth);
    vi.spyOn(TestBed.inject(Router), "navigate").mockResolvedValue(true);
    login();
    api.dashboard().subscribe({ error: () => {} });
    http
      .expectOne("/api/v1/admin/tenants/NTM/ntm/dashboard")
      .flush({}, { status: 403, statusText: "Forbidden" });
    expect(auth.token()).toBe("test-token");
    vi.advanceTimersByTime(900000);
    expect(auth.token()).toBe("test-token");
    vi.advanceTimersByTime(899999);
    expect(auth.token()).toBe("test-token");
    vi.advanceTimersByTime(1);
    expect(auth.token()).toBeNull();
    login();
    api.dashboard().subscribe({ error: () => {} });
    http
      .expectOne("/api/v1/admin/tenants/NTM/ntm/dashboard")
      .flush({}, { status: 401, statusText: "Unauthorized" });
    expect(auth.token()).toBeNull();
  });
  it("does not restore a session from a late login after logout", () => {
    const auth = TestBed.inject(Auth);
    const error = vi.fn();
    auth.login("admin@example.test", "test-password").subscribe({ error });
    auth.clear();
    http
      .expectOne("/api/v1/auth/login")
      .flush({ accessToken: "old-token", tokenType: "Bearer", expiresIn: 1800 });
    expect(auth.token()).toBeNull();
    expect(error).toHaveBeenCalled();
  });
});
