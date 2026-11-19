import { beforeEach, describe, expect, it, vi } from "vitest";
import { TestBed } from "@angular/core/testing";
import { provideHttpClient } from "@angular/common/http";
import { HttpTestingController, provideHttpClientTesting } from "@angular/common/http/testing";
import { provideRouter } from "@angular/router";
import { CONFIG } from "../core/config";
import { Auth } from "../core/auth";
import { Administrators } from "./administrators";

describe("administrator password forms", () => {
  let http: HttpTestingController;
  beforeEach(() => {
    sessionStorage.clear();
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
      { provide: CONFIG, useValue: { apiBaseUrl: "/api/v1", tenantCode: "NTM" } }] });
    http = TestBed.inject(HttpTestingController);
  });
  it("blocks mismatched confirmation and sends a valid own-password change", () => {
    const fixture = TestBed.createComponent(Administrators);
    const page = fixture.componentInstance;
    http.expectOne(r => r.url === "/api/v1/admin-users").flush({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    const logout = vi.spyOn(TestBed.inject(Auth), "logout").mockImplementation(() => {});
    page.changeOwn();
    page.form.patchValue({ currentPassword: "old-password", password: "newpass1!", confirmPassword: "different" });
    page.save();
    http.expectNone("/api/v1/auth/change-password");
    page.form.controls.confirmPassword.setValue("newpass1!");
    page.save();
    const req = http.expectOne("/api/v1/auth/change-password");
    expect(req.request.body).toEqual({ currentPassword: "old-password", newPassword: "newpass1!", confirmPassword: "newpass1!" });
    req.flush(null);
    expect(logout).toHaveBeenCalled();
    http.verify();
  });
});
