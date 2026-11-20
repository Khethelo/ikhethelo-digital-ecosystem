See [current workflow and email setup](../docs/REGISTRATION_WORKFLOW.md). This supersedes historical workflow descriptions below.

# Es'hlahleni Social Club frontend

## Run in VS Code

Open the parent **ikhethelo-digital** folder in VS Code (the folder containing both projects). Open **Run and Debug** with **Ctrl+Shift+D**, select **Run frontend + backend**, then click the green play button or press **F5**. Both servers start in integrated terminals, and the site opens at **http://localhost:4200** when Angular is ready. Use the debug toolbar's Stop button (**Shift+F5**) to stop both sessions. This launcher runs the API without attaching a C# debugger.

PostgreSQL must already be running, and the API's development secrets and database must already be configured; see the backend README for first-time setup.

Alternatively, start each server manually:

From the workspace root, start the existing API in one terminal:

```powershell
dotnet run --project .\IKhethelo.Digital.Api\src\IKhethelo.Digital.Api --launch-profile IKhethelo.Digital.Api
```

In a second terminal:

```powershell
.\eshlahleni-frontend\Start-Frontend.ps1
```

Open **http://localhost:4200**. Public applications are at `/join`; existing active administrators sign in at `/admin/login`. No credentials are included in this project. The helper uses installed Node or the workspace's portable Node if available. If PowerShell script execution is restricted, use npm directly.

On another machine install Node 24.15+ in the 24.x line, then:

```powershell
cd eshlahleni-frontend
npm ci
npm start
```

## Configuration and architecture

Angular core 22.2.4, CLI/build 22.2.6, strict TypeScript/templates, standalone components, lazy routes, reactive forms and SCSS. Exact dependency versions are in `package-lock.json`. Built with Node 24.22.2. No global Angular CLI is required.

Both `src/environments/` files use `/api/v1`. `proxy.conf.cjs` forwards local development `/api/**` calls to `https://localhost:44385`, the IIS Express HTTPS address. Set `ESHL_API_TARGET` before `npm start` to select another loopback API port, such as `https://localhost:57972` for the project launch profile. Certificate checking is disabled only for this development proxy. Requests through the proxy are same-origin; no backend CORS change is needed. Existing Development CORS also allows localhost:4200.

Production: run `npm run build` and serve `dist/eshlahleni-frontend/browser` through an HTTPS web server. Reverse-proxy `/api/*` to the API and fall back to `index.html` for other non-file routes. Never deploy the development server/proxy. Keep database settings, RSA keys, credentials and JWTs out of Angular configuration. `npm run preview` is a loopback-only production-build preview for local tests, not production hosting.

Core services and DTOs live in `src/app/core/`; reusable field/error/status/dialog/pagination and validation code live in `shared/`. Public and admin screens are under `features/public/` and `features/admin/`. Routes are declared in `app.routes.ts`.

Public routes: `/`, `/about`, `/membership`, `/join`, `/faq`, `/contact`, `/membership-terms`, `/privacy`. Admin routes: `/admin/login`, `/admin`, `/admin/applications`, `/admin/applications/:applicationId`, `/admin/members`, `/admin/members/:memberId`.

## Membership and authentication

The public form submits only names, calendar date of birth, canonical international phone, email and both consent flags. Success displays the registration reference, Awaiting In-Person Completion status, confirmed venue address and WhatsApp CTA. Staff confirm the R100 step was completed in person and activate membership through a confirmation dialog. Activation displays the returned member number. Members support search, status filters, pagination, detail, profile edits and Active/Suspended/Inactive changes.

Integrated endpoints, relative to `/api/v1`:

- POST `/public/eshlahleni/membership-applications`
- POST `/auth/login`
- GET `/admin/tenants/ESHL/membership-applications` and `/{id}`
- POST registration `/{id}/activate`
- GET `/admin/tenants/ESHL/members` and `/{id}`
- PUT member `/{id}` and `/{id}/status`

The API enforces ownership. No tenant UUID is accepted or displayed. There are no category, product, payment, applicant-account or member-login features. Review notes are sent but not invented in history: the application response does not return review metadata.

JWTs stay only in service memory for 900 seconds. Refresh signs out. Expiry and protected 401 responses clear the session and redirect to login. The interceptor attaches tokens only to configured admin API calls. There is no persistent token storage or refresh-token flow and no token logging/decoding.

Birth dates stay `yyyy-MM-dd`; adulthood uses the South African calendar and eighteenth-birthday semantics, including leap-day birthdays. Names count Unicode code points; phone numbers require canonical E.164. The API remains authoritative. Forms retain input after failures, map field errors and explain conflicts, unavailable/forbidden records, connectivity and rate limiting. Repeated mutations are disabled while saving.

## Branding and dummy content

The original logo is copied unchanged to `src/assets/branding/eshlahleni-logo.jpeg`. Black/white branding, charcoal, warm paper, olive accents and bold system display typography carry through public and admin screens. No external font service or product imagery is used.

Confirmed contact details are centralised in `src/app/content/club-content.ts`: WhatsApp 083 726 5910 and 32 Albany Grove, Durban Central, Durban, 4001. No public email address or opening hours are displayed. Registration instructions explain the R100 in-person step. Draft terms/privacy remain in `features/public/info-pages.ts`, with consent wording in `join.ts`; the owner must finalise policy text before public launch.

Routes update document title, description and Open Graph text. Admin, draft-policy and error pages are noindex. `public/robots.txt` excludes admin. When a real HTTPS domain is confirmed, run `npm run sitemap -- https://your-confirmed-domain`, then rebuild. No production domain is invented. The site is client rendered; prerendering can be added for stronger crawler support.

## Verification

```powershell
npm test
npm run build
```

Browser tests use Playwright and installed Chrome. Override the Windows default with `CHROME_PATH`. With the frontend running, `npm run test:e2e` runs public responsiveness and mocked-error tests; real-flow tests deliberately skip without isolated credentials.

For complete real-API tests, start an isolated PostgreSQL test cluster, build the existing backend Release binary, build the frontend, then:

```powershell
$env:IKD_TEST_POSTGRES = 'Host=127.0.0.1;Port=55439;Database=postgres;Username=postgres'
npm run verify:live
```

Use an isolated test cluster, never the club database. Requires .NET 10, backend dependencies and a test role allowed to create databases. `scripts/verification/` references existing infrastructure without editing it. The harness creates a random disposable database, applies existing migrations, bootstraps a temporary admin, starts an isolated API using temporary RSA keys, serves the production build, tests Chrome workflows and drops only its generated database. No backend source/configuration is changed. API logs and credentials are withheld; browser traces/video are disabled. Screenshots in `artifacts/` contain only disposable test data.

See `IMPLEMENTATION_REPORT.md` for delivered scope, verification results and launch requirements.
