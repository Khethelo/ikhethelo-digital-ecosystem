import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { TestBed } from "@angular/core/testing";
import { provideHttpClient } from "@angular/common/http";
import {
  provideHttpClientTesting,
  HttpTestingController,
} from "@angular/common/http/testing";
import {
  ActivatedRoute,
  Router,
  convertToParamMap,
  provideRouter,
} from "@angular/router";
import { CONFIG } from "../core/config";
import { BusinessForm } from "./business-form";

describe("business assessment ownership and preservation", () => {
  let http: HttpTestingController;
  const business = {
    id: "b1",
    veteranId: "v1",
    businessName: "Original business",
    operationalStatus: null,
    numberOfEmployees: null,
    yearsInOperation: null,
    fundingRequired: false,
    fundingAmount: 0,
    assessmentDate: "2026-10-13",
    operationalStatusSource: " Operationl ",
    numberOfEmployeesSource: "3 part time\n",
    yearsInOperationSource: "Needs training",
    fundingRequiredSource: "Funding",
    sourceRecordNumber: 3,
  };
  const veteran = {
    id: "v1",
    fullName: "Veteran",
    mkmvaNumber: "MK-1",
    business: { id: "b1", businessName: "Original business" },
  };
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: CONFIG,
          useValue: { apiBaseUrl: "/api/v1", tenantCode: "NTM" },
        },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ id: "b1" }),
              queryParamMap: convertToParamMap({}),
            },
          },
        },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function existing() {
    const fixture = TestBed.createComponent(BusinessForm);
    http
      .expectOne("/api/v1/admin/tenants/NTM/ntm/businesses/b1")
      .flush(business);
    http.expectOne("/api/v1/admin/tenants/NTM/ntm/veterans/v1").flush(veteran);
    return fixture;
  }
  it("retains every original source field during full PUT even if a form source control is changed", () => {
    const fixture = existing();
    const page = fixture.componentInstance;
    vi.spyOn(TestBed.inject(Router), "navigate").mockResolvedValue(true);
    page.form.controls.numberOfEmployeesSource.setValue("accidental change");
    page.form.controls.businessName.setValue("Updated business");
    page.save();
    const req = http.expectOne("/api/v1/admin/tenants/NTM/ntm/businesses/b1");
    expect(req.request.method).toBe("PUT");
    for (const key of [
      "operationalStatusSource",
      "numberOfEmployeesSource",
      "yearsInOperationSource",
      "fundingRequiredSource",
      "sourceRecordNumber",
    ] as const)
      expect(req.request.body[key]).toBe(business[key]);
    expect(req.request.body.fundingRequired).toBe(false);
    expect(req.request.body.fundingAmount).toBe(0);
    expect(req.request.body.assessmentDate).toBe("2026-10-13");
    req.flush({ ...business, businessName: "Updated business" });
  });
  it("prevents a second business for a veteran already linked to a different business", () => {
    const fixture = existing();
    const page = fixture.componentInstance;
    page.selected.set({
      ...veteran,
      business: { id: "different", businessName: "Existing" },
    } as any);
    expect(page.canAssign()).toBe(false);
    page.save();
    http.expectNone((r) => r.method === "PUT");
  });
  it("keeps form data and the conflict visible when assignment races", () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: CONFIG,
          useValue: { apiBaseUrl: "/api/v1", tenantCode: "NTM" },
        },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({}),
              queryParamMap: convertToParamMap({}),
            },
          },
        },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    const fixture = TestBed.createComponent(BusinessForm);
    const page = fixture.componentInstance;
    http
      .expectOne((r) => r.url.endsWith("/veterans"))
      .flush({ items: [], page: 1, pageSize: 20, totalCount: 0 });
    page.selected.set({ ...veteran, business: null } as any);
    page.form.patchValue({
      veteranId: "v1",
      businessName: "Unsaved business",
      numberOfEmployeesSource: "3 part time",
    });
    page.save();
    http
      .expectOne("/api/v1/admin/tenants/NTM/ntm/businesses")
      .flush({}, { status: 409, statusText: "Conflict" });
    http.expectOne("/api/v1/admin/tenants/NTM/ntm/veterans/v1").flush(veteran);
    expect(page.issue()?.status).toBe(409);
    expect(page.form.controls.businessName.value).toBe("Unsaved business");
    expect(page.form.controls.numberOfEmployeesSource.value).toBe(
      "3 part time",
    );
    expect(page.canAssign()).toBe(false);
  });
});
