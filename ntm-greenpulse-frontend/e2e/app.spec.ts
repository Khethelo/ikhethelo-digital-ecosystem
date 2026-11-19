import { test, expect, Page } from "@playwright/test";
import fs from "node:fs/promises";
const v1 = "11111111-1111-4111-8111-111111111111",
  v2 = "22222222-2222-4222-8222-222222222222",
  b1 = "33333333-3333-4333-8333-333333333333",
  a1 = "44444444-4444-4444-8444-444444444444";
const time = "2026-11-13T08:00:00Z";
async function setup(page: Page) {
  const veterans = Array.from({ length: 24 }, (_, i) => ({
    id: i === 0 ? v1 : i === 1 ? v2 : `veteran-${i + 1}`,
    fullName:
      i === 0
        ? "Thabo Example"
        : i === 1
          ? "Nomsa Example"
          : `Veteran ${i + 1}`,
    mkmvaNumber: `MK-${i + 1}`,
    dmvNumber: null,
    contactNumber: "Recorded contact",
    emailAddress: "recorded source contact",
    membershipReference: " DMV Database ",
    createdAtUtc: time,
    updatedAtUtc: time,
    business:
      i === 0 ? { id: b1, businessName: "Green Fields Cooperative" } : null,
  }));
  const businesses: any[] = [
    {
      id: b1,
      veteranId: v1,
      businessName: "Green Fields Cooperative",
      cipcRegistrationNumber: "CIPC-001",
      businessAddress: "Durban",
      sector: "Farming",
      businessBackground: "Crop cultivation and local supply.",
      operationalStatus: null,
      numberOfEmployees: null,
      yearsInOperation: 1.5,
      currentBusinessNeeds: "Equipment and skills training",
      priorityNeed: "Equipment",
      fundingRequired: false,
      fundingAmount: 0,
      additionalComments: "Current recorded assessment.",
      assessmentDate: "2026-10-13",
      assessedBy: null,
      createdAtUtc: time,
      updatedAtUtc: time,
      sourceRecordNumber: 7,
      operationalStatusSource: " Operationl ",
      numberOfEmployeesSource: " 3 part time\n",
      yearsInOperationSource: "Needs training",
      fundingRequiredSource: "Funding",
    },
  ];
  const admins: any[] = [
    {
      id: a1,
      email: "admin@example.test",
      role: "TenantAdmin",
      isActive: true,
      createdAtUtc: time,
      updatedAtUtc: time,
      lastLoginAtUtc: time,
    },
  ];
  const state = {
    veterans,
    businesses,
    admins,
    requests: [] as {
      method: string;
      path: string;
      body: any;
      query: URLSearchParams;
    }[],
    conflict: false,
    denyAdmins: false,
    failDashboard: false,
    unauthorized: false,
  };
  const token = `header.${Buffer.from(JSON.stringify({ sub: a1, role: "TenantAdmin" })).toString("base64url")}.signature`;
  await page.route("**/api/v1/**", async (route) => {
    const req = route.request(),
      url = new URL(req.url()),
      path = url.pathname,
      method = req.method(),
      body = req.postDataJSON();
    state.requests.push({ method, path, body, query: url.searchParams });
    const send = (data: any, status = 200) =>
      route.fulfill({
        status,
        contentType: "application/json",
        body: JSON.stringify(data),
      });
    if (path.endsWith("/auth/login"))
      return send({ accessToken: token, tokenType: "Bearer", expiresIn: 1800 });
    if (
      req.headers()["authorization"] !== `Bearer ${token}` ||
      state.unauthorized
    )
      return send({ title: "Authentication required" }, 401);
    if (path.endsWith("/dashboard")) {
      if (state.failDashboard) return send({}, 503);
      return send({
        totalVeterans: veterans.length,
        totalBusinesses: businesses.length,
        operationalBusinesses: 0,
        partiallyOperationalBusinesses: 0,
        notOperationalBusinesses: 0,
        totalRecordedFundingAmount: 0,
        fundingRequiredBusinesses: 0,
        businessesBySector: [{ value: "Farming", count: businesses.length }],
        recordedPriorityNeeds: [
          { value: "Equipment", count: businesses.length },
        ],
      });
    }
    const paginate = (items: any[]) => {
      const number = Number(url.searchParams.get("page") || 1),
        size = Number(url.searchParams.get("pageSize") || 20);
      return {
        items: items.slice((number - 1) * size, number * size),
        page: number,
        pageSize: size,
        totalCount: items.length,
      };
    };
    if (path.includes("/admin-users")) {
      if (state.denyAdmins) return send({}, 403);
      const parts = path.split("/admin-users")[1].split("/").filter(Boolean),
        id = parts[0];
      if (method === "GET")
        return send(
          paginate(
            admins.filter(
              (a) =>
                !url.searchParams.get("search") ||
                a.email.includes(url.searchParams.get("search")!),
            ),
          ),
        );
      if (!id) {
        const admin = { ...admins[0], id: "new-admin", email: body.email };
        admins.push(admin);
        return send(admin, 201);
      }
      const admin = admins.find((a) => a.id === id);
      if (!admin) return send({}, 404);
      if (parts[1] === "reset-password") return route.fulfill({ status: 204 });
      Object.assign(
        admin,
        method === "PATCH"
          ? { isActive: body.isActive }
          : { email: body.email },
      );
      return send(admin);
    }
    const match = path.match(/\/(veterans|businesses)(?:\/([^/]+))?$/);
    if (!match) return send({}, 404);
    const kind = match[1],
      id = match[2],
      rows: any[] = kind === "veterans" ? veterans : businesses;
    if (method === "GET" && !id) {
      let items = [...rows];
      const search = (url.searchParams.get("search") || "").toLowerCase();
      if (search)
        items = items.filter((x) =>
          (kind === "veterans"
            ? `${x.fullName} ${x.mkmvaNumber} ${x.dmvNumber}`
            : x.businessName
          )
            .toLowerCase()
            .includes(search),
        );
      if (kind === "veterans" && url.searchParams.has("hasBusiness"))
        items = items.filter(
          (x) =>
            !!x.business === (url.searchParams.get("hasBusiness") === "true"),
        );
      for (const key of ["sector", "operationalStatus", "veteranId"])
        if (url.searchParams.get(key))
          items = items.filter((x) => x[key] === url.searchParams.get(key));
      return send(paginate(items));
    }
    if (method === "GET") {
      const row = rows.find((x) => x.id === id);
      return row ? send(row) : send({}, 404);
    }
    if (method === "POST" && kind === "businesses" && state.conflict) {
      veterans.find((x) => x.id === body.veteranId)!.business = {
        id: b1,
        businessName: "Existing business",
      };
      return send({ title: "Conflict" }, 409);
    }
    if (method === "POST") {
      const row = {
        ...body,
        id: kind === "veterans" ? "new-veteran" : "new-business",
        createdAtUtc: time,
        updatedAtUtc: time,
        ...(kind === "veterans" ? { business: null } : {}),
      };
      rows.push(row);
      if (kind === "businesses")
        veterans.find((x) => x.id === body.veteranId)!.business = {
          id: row.id,
          businessName: row.businessName,
        };
      return send(row, 201);
    }
    const row = rows.find((x) => x.id === id);
    if (!row) return send({}, 404);
    Object.assign(row, body);
    return send(row);
  });
  return state;
}
async function login(page: Page, destination = "/dashboard") {
  await page.goto(destination);
  await expect(page).toHaveURL(/\/login/);
  await page
    .getByRole("textbox", { name: "Email address", exact: true })
    .fill("admin@example.test");
  await page
    .getByLabel("Password", { exact: true })
    .fill("not-a-real-password");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).toHaveURL(
    new RegExp(destination.replace(/[.*+?^${}()|[\]\\]/g, "\\$&") + "$"),
  );
}
test("sign-in guard, real aggregate display, sign-out and no persisted tokens", async ({
  page,
}) => {
  await setup(page);
  await login(page);
  await expect(
    page.getByRole("heading", { name: "Overview", exact: true }),
  ).toBeVisible();
  await expect(page.getByText("24", { exact: true })).toBeVisible();
  await expect(page.getByText("Farming", { exact: true })).toBeVisible();
  expect(
    await page.evaluate(() => localStorage.length + sessionStorage.length),
  ).toBe(0);
  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page).toHaveURL(/\/login$/);
  await page.goto("/veterans");
  await expect(page).toHaveURL(/\/login/);
});
test("veteran search, filtering and server pagination", async ({ page }) => {
  const state = await setup(page);
  await login(page, "/veterans");
  await expect(page.getByRole("table").getByRole("row")).toHaveCount(21);
  await page.getByRole("button", { name: "Next page" }).click();
  await expect(page.getByText("Page 2 of 2", { exact: false })).toBeVisible();
  await page.getByLabel("Search veterans", { exact: true }).fill("Nomsa");
  await page.getByLabel("Business assignment").selectOption("no");
  await page.getByRole("button", { name: "Search", exact: true }).click();
  await expect(page.getByRole("table").getByRole("row")).toHaveCount(2);
  const query = state.requests
    .filter((r) => r.method === "GET" && r.path.endsWith("/veterans"))
    .at(-1)!.query;
  expect(query.get("search")).toBe("Nomsa");
  expect(query.get("hasBusiness")).toBe("false");
  expect(query.get("page")).toBe("1");
});
test("veteran creation requires either identifier without a membership reference field", async ({
  page,
}) => {
  const state = await setup(page);
  await login(page, "/veterans/new");
  await page.getByLabel("Full name").fill("New veteran");
  await page.getByRole("button", { name: "Create veteran" }).click();
  await expect(
    page.getByText(
      "Provide at least one actual MKMVA or DMV registration number.",
    ),
  ).toBeVisible();
  expect(
    state.requests.some(
      (r) => r.method === "POST" && r.path.endsWith("/veterans"),
    ),
  ).toBe(false);
  await page.getByLabel("DMV registration number").fill("DMV-NEW");
  await expect(page.getByLabel("Original membership reference")).toHaveCount(0);
  await page.getByRole("button", { name: "Create veteran" }).click();
  await expect(page).toHaveURL(/\/veterans$/);
  const request = state.requests.find(
    (r) => r.method === "POST" && r.path.endsWith("/veterans"),
  )!;
  expect(request.body.membershipReference).toBeNull();
  expect(request.body.dmvNumber).toBe("DMV-NEW");
  expect(request.body.tenantId).toBeUndefined();
});
test("linked veterans retain hidden source references when updated", async ({
  page,
}) => {
  const state = await setup(page);
  await login(page, `/veterans/${v1}`);
  await expect(
    page.getByRole("link", { name: "Open business", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("link", { name: "Add business", exact: true }),
  ).toHaveCount(0);
  await expect(page.getByLabel("Original membership reference")).toHaveCount(0);
  await page.getByLabel("Full name").fill("Updated veteran");
  await page.getByRole("button", { name: "Save changes", exact: true }).click();
  await expect(page).toHaveURL(/\/veterans$/);
  const request = state.requests.find(
    (r) => r.method === "PUT" && r.path.endsWith(`/${v1}`),
  )!;
  expect(request.body.membershipReference).toBe(" DMV Database ");
  await login(page, `/veterans/${v1}`);
  await page.getByRole("link", { name: "Open business", exact: true }).click();
  await expect(page).toHaveURL(new RegExp(`/businesses/${b1}$`));
});
test("assessment updates omit source text and retain false, zero and date", async ({
  page,
}) => {
  const state = await setup(page);
  await login(page, `/businesses/${b1}`);
  await expect(page.getByText("Original workbook values")).toHaveCount(0);
  await expect(
    page.getByLabel("Number of employees", { exact: true }),
  ).toHaveValue("");
  await page
    .getByRole("textbox", { name: "Business name", exact: true })
    .fill("Updated business");
  await page.getByRole("button", { name: "Save assessment" }).click();
  await expect(page).toHaveURL(/\/businesses$/);
  const request = state.requests.find(
    (r) => r.method === "PUT" && r.path.endsWith(`/${b1}`),
  )!;
  expect(request.body.numberOfEmployeesSource).toBeUndefined();
  expect(request.body.operationalStatusSource).toBeUndefined();
  expect(request.body.yearsInOperationSource).toBeUndefined();
  expect(request.body.fundingRequiredSource).toBeUndefined();
  expect(request.body.fundingRequired).toBe(false);
  expect(request.body.fundingAmount).toBe(0);
  expect(request.body.assessmentDate).toBe("2026-10-13");
  expect(request.body.numberOfEmployees).toBeNull();
});
test("available-owner lookup is paginated and business creation uses selected owner", async ({
  page,
}) => {
  const state = await setup(page);
  await login(page, "/businesses/new");
  await page.getByRole("button", { name: "Next page" }).click();
  await expect(page.getByText("Page 2 of 2", { exact: false })).toBeVisible();
  await page.getByRole("button", { name: /Veteran 24.*Select/ }).click();
  await page
    .getByRole("textbox", { name: "Business name", exact: true })
    .fill("New business");
  await page
    .getByRole("button", { name: "Create business", exact: true })
    .click();
  await expect(page).toHaveURL(/\/businesses$/);
  const request = state.requests.find(
    (r) => r.method === "POST" && r.path.endsWith("/businesses"),
  )!;
  expect(request.body.veteranId).toBe("veteran-24");
  expect(request.body.fundingRequired).toBeNull();
  expect(request.body.numberOfEmployees).toBeNull();
});
test("assignment race keeps unsaved work and exposes the existing business", async ({
  page,
}) => {
  const state = await setup(page);
  state.conflict = true;
  await login(page, `/businesses/new?veteranId=${v2}`);
  await page
    .getByRole("textbox", { name: "Business name", exact: true })
    .fill("Unsaved name");
  await page
    .getByRole("button", { name: "Create business", exact: true })
    .click();
  await expect(
    page.getByText("Your entered values have been kept.", { exact: false }),
  ).toBeVisible();
  await expect(
    page.getByRole("textbox", { name: "Business name", exact: true }),
  ).toHaveValue("Unsaved name");
  await expect(
    page.getByRole("link", { name: "Open Existing business" }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Create business", exact: true }),
  ).toBeDisabled();
});
test("numeric precision is rejected without silently rounding", async ({
  page,
}) => {
  const state = await setup(page);
  await login(page, `/businesses/${b1}`);
  await page
    .getByLabel("Estimated funding amount (R)", { exact: true })
    .fill("12.345");
  await page.getByRole("button", { name: "Save assessment" }).click();
  await expect(page.getByText("Use at most 2 decimal places.")).toBeVisible();
  expect(state.requests.some((r) => r.method === "PUT")).toBe(false);
});
test("admin management reuses scoped endpoints and protects self-deactivation", async ({
  page,
}) => {
  const state = await setup(page);
  await login(page, "/administrators");
  await expect(
    page.getByRole("button", { name: "Deactivate", exact: true }),
  ).toBeDisabled();
  await page
    .getByRole("button", { name: "Add administrator", exact: false })
    .click();
  await page
    .getByRole("textbox", { name: "Administrator email", exact: true })
    .fill("second@example.test");
  await page.getByLabel("Initial password").fill("new-test-password");
  await page.getByRole("button", { name: "Save administrator" }).click();
  await expect(
    page.getByText("second@example.test", { exact: true }),
  ).toBeVisible();
  const request = state.requests.find(
    (r) => r.method === "POST" && r.path.endsWith("/admin-users"),
  )!;
  expect(request.query.get("tenantCode")).toBe("NTM");
  expect(request.body.role).toBe("TenantAdmin");
  expect(request.body.tenantId).toBeUndefined();
  const row = page.getByRole("row").filter({ hasText: "second@example.test" });
  await row.getByRole("button", { name: "Reset password" }).click();
  await page.getByLabel("New password").fill("replacement-test-password");
  await page.getByRole("button", { name: "Save administrator" }).click();
  await expect(
    page.getByText("Password updated.", { exact: false }),
  ).toBeVisible();
  expect(
    state.requests.some(
      (r) =>
        r.path.endsWith("/reset-password") &&
        r.body.newPassword === "replacement-test-password",
    ),
  ).toBe(true);
});
test("403 stays signed in and 401 redirects to sign-in", async ({ page }) => {
  const state = await setup(page);
  await login(page);
  state.denyAdmins = true;
  await page.getByRole("link", { name: "Administrators", exact: true }).click();
  await expect(
    page.getByText("This account cannot access this area.", { exact: false }),
  ).toBeVisible();
  await expect(page).toHaveURL(/\/administrators$/);
  state.unauthorized = true;
  await page.getByRole("link", { name: "Veterans", exact: true }).click();
  await expect(page).toHaveURL(/\/login/);
  await expect(
    page.getByText("Your session has ended.", { exact: false }),
  ).toBeVisible();
});
test("dashboard errors never display manufactured zero totals", async ({
  page,
}) => {
  const state = await setup(page);
  await login(page);
  state.failDashboard = true;
  await page.getByRole("button", { name: "Refresh", exact: true }).click();
  await expect(
    page.getByText("The service could not complete this request.", {
      exact: false,
    }),
  ).toBeVisible();
  await expect(page.getByText("Total veterans", { exact: true })).toHaveCount(
    0,
  );
});
test("compact desktop and mobile views have no page overflow", async ({
  page,
}) => {
  await setup(page);
  await page.setViewportSize({ width: 1440, height: 1000 });
  await login(page);
  await fs.mkdir("../.verification/ntm-ui", { recursive: true });
  await page.screenshot({
    path: "../.verification/ntm-ui/dashboard-desktop.png",
    fullPage: true,
  });
  await page.setViewportSize({ width: 390, height: 844 });
  await expect(
    page.getByRole("button", { name: "Toggle navigation" }),
  ).toBeVisible();
  await expect
    .poll(
      () =>
        page.evaluate(
          () => document.documentElement.scrollWidth <= window.innerWidth,
        ),
      { message: "Viewport overflow", timeout: 5000 },
    )
    .toBe(true);
  await page.screenshot({
    path: "../.verification/ntm-ui/dashboard-mobile.png",
    fullPage: true,
  });
  await page.getByRole("button", { name: "Toggle navigation" }).click();
  await page.getByRole("link", { name: "Businesses", exact: true }).click();
  await expect(
    page.getByRole("heading", { name: "Businesses", exact: true }),
  ).toBeVisible();
  await expect(page.getByRole("table")).toBeVisible();

  await expect
    .poll(
      () =>
        page.evaluate(
          () => document.documentElement.scrollWidth <= window.innerWidth,
        ),
      { message: "Viewport overflow", timeout: 5000 },
    )
    .toBe(true);
});
