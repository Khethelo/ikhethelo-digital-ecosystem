# NTM GreenPulse Angular frontend — 13 November 2026

Subsequent UI update: the original membership reference is no longer displayed in the veteran form or directory. Existing values remain stored and are preserved verbatim on updates; new records submit a null reference. Shared API access tokens and the NTM session now last 30 minutes (1,800 seconds). Restart the API and sign in again to receive the new lifetime. Sessions remain in memory, so refreshing the page still ends the session. The verification results below describe the original implementation run.

Verification of this subsequent update: Angular contract check, production build, 18 unit tests and two targeted browser checks passed. The browser checks confirm field absence, new-record null values, preservation of existing hidden references and linked-business navigation. Unit tests verify that sessions remain active at 15 minutes and immediately before 30 minutes, then expire at 30 minutes. Backend Release build passed with zero warnings/errors. The new backend integration run was blocked during discovery by Windows Application Control loading `IKhethelo.Digital.Application.dll` (`0x800711C7`); no passing backend test result is claimed for this update. The isolated PostgreSQL test cluster was stopped; the development database was not used for these tests. Backend diagnostics are in `.verification/ntm-session-tests.log`.

The complete compact administrator application is implemented in **`ntm-greenpulse-frontend/`**. It is a separate Angular workspace using standalone components, Bootstrap 5, SCSS, Reactive Forms and typed services. No Es'hlahleni frontend file was modified or redeployed during this implementation; pre-existing workspace changes were left intact. No production migration, live record import, live administrator provisioning or deployment was performed.

## Implemented experience

| Route | Functionality |
| --- | --- |
| `/login` | Existing JWT login API, NTM authorization check, safe return navigation, in-memory session, expiry/sign-out handling |
| `/dashboard` | Real backend totals, validated status counts, recorded funding, sector breakdowns, priority needs, loading/error/retry states |
| `/veterans` | Server-side name/registration search, with/without-business filters, pagination and page size |
| `/veterans/new`, `/veterans/:id` | Create/edit identities and source contact information, either-registration validation, original membership reference and linked business |
| `/businesses` | Business-name search, exact sector/status filtering, contextual veteran filter, server pagination |
| `/businesses/new`, `/businesses/:id` | Paginated available-veteran selection, business profile, operations, current needs/funding and assessment metadata |
| `/administrators` | Existing tenant-scoped administrator list/search/pagination, creation, email editing, activation/deactivation and password reset endpoints |

The layout uses an independent NTM sidebar, compact panels/tables and responsive mobile navigation. Table overflow stays inside its scroll container. Screen headings, form labels, errors, loading states and keyboard focus indicators are present. Original values are visibly separated from validated values. Desktop/mobile screenshot artifacts with synthetic test records are in `.verification/ntm-ui/`; these are verification fixtures, not live network statistics.

## Contract and security

The implementation follows the actual `NtmContracts.cs`, OpenAPI v1.2.0, NTM module documentation and verified workbook mapping. The repository contains no separate NTM frontend master-prompt document; the explicit frontend request and implemented backend are authoritative. Es'hlahleni master-prompt content was not repurposed into the new NTM experience.

All nine NTM HTTP operations use `/api/v1/admin/tenants/{tenantCode}/ntm`. Authentication uses `/api/v1/auth/login`; administrator management uses `/api/v1/admin-users` with the configured canonical `tenantCode` query parameter. Only TenantAdmin is assigned through account management. Internal account-management and NTM grants remain separate backend policies. Browser claims are display hints only, never authorization decisions.

`public/config.json` defaults to the same-origin `/api/v1` base and NTM code, without secrets or UUIDs. Tokens are kept only in memory, sent only to configured protected API routes, cleared on sign-out/expiry/401 and never stored in localStorage/sessionStorage. A 403 is shown as an authorization error without inventing access or automatically signing out. Refreshing requires sign-in again.

Services whitelist mutation properties: ownership, status changes outside DTO scope and extra frontend state cannot leak into bodies. Server errors remain authoritative; field errors are displayed. Unsaved edits prompt before ordinary navigation, while expired sessions return to login. No public registration, veteran accounts, import/export, assessment history, deletion, notifications or unrelated workflows are introduced.

## Source preservation and one-business rule

- Existing `MembershipReference` remains separate from verified MKMVA/DMV identifiers and is retained verbatim.
- `OperationalStatusSource`, `NumberOfEmployeesSource`, `YearsInOperationSource`, `FundingRequiredSource` and `SourceRecordNumber` are displayed separately, read-only on existing records, and explicitly copied from the loaded record on full PUT. They cannot be accidentally erased by editing validated fields.
- Original employee qualifiers, unknown funding descriptions and status typos never populate typed values automatically. Nullable fields stay unknown rather than becoming false or zero. Zero, false and ISO calendar dates are retained correctly.
- Numeric input uses Angular's number value accessor; integer counts, one-decimal years and two-decimal Rand amounts are validated without rounding. Backend limits and required registration rules are respected.
- Veterans with linked businesses open that existing record. New-business selection queries only available veterans and supports search/pagination beyond the first page. A fresh veteran detail read checks assignment before enabling save. Concurrent backend 409 responses retain entered form data, refresh the assignment and expose the existing business link. Database constraints remain the final authority.

## Files and configuration

All application additions are under `ntm-greenpulse-frontend/`:

- Angular/package/TypeScript configuration and reproducible `package-lock.json`.
- `public/config.json`, `proxy.conf.cjs`, `src/main.ts`, `src/index.html`.
- `src/app/app.ts`: root shell, lazy routes, authentication and unsaved-change guards.
- `src/app/core/`: typed models, exact API services, session handling, configuration validation, forms/error helpers and cancellable server-list state.
- `src/app/shared/ui.ts`: reusable accessible fields, issue display and pagination.
- `src/app/pages/`: login, dashboard, directories, veteran/business forms and administrator management.
- `src/styles.scss`: independent compact Bootstrap-based visual system and responsive layouts.
- Unit tests, `e2e/app.spec.ts`, Playwright configuration, contract checker and an owned local static test preview.
- README with local use, backend setup, checks and hosting requirements; project-local ignores for dependency/build/test artifacts.

Shared documentation changes are this report and completion of the backend report's stale pending-verification section. Backend source/schema were not changed as part of this frontend implementation.

## Executed verification

| Check | Final result |
| --- | --- |
| `npm run check:contract` | Passed: NTM/admin DTO property sets, full write whitelists and nine NTM operations match the actual OpenAPI |
| `npm run build` | Passed: production output generated, approximately 536 kB initial raw / 106 kB estimated transfer, within configured budgets |
| `npm test` | **18 passed, 0 failed**, across three files |
| `npm run test:e2e` | **12 passed, 0 failed**, installed Chrome with synthetic local API fixtures |
| `git diff --check` | Passed |
| Fresh backend Release build | Passed, zero warnings/errors |
| Fresh PostgreSQL backend rerun | **85 passed, 0 failed, 0 skipped**, 1 minute 59 seconds |

Browser checks exercise sign-in/guard/sign-out, absence of persisted tokens, real-response statistics rendering, search/filtering/pagination, veteran creation, existing-business navigation, source-preserving full updates, available-owner pagination, duplicate-assignment races, numeric precision, administrator creation/password reset, 403/401 handling, dashboard error behavior and desktop/mobile overflow. Unit coverage includes configuration/return-URL safety, API scope/payloads, session expiry/races, source preservation and native numeric input conversion.

Verification found and fixed dynamic numeric fields producing string values and an absolutely positioned hidden table label escaping its mobile scroll container. The final full browser run passes after both fixes. Browser fixtures do not replace the separate real PostgreSQL API/constraint/concurrency tests. No live frontend-to-production verification or customer-data screenshot is claimed.

Angular's builders and Chrome were run with approved normal local filesystem access because the workspace sandbox blocks dependency/ancestor resolution. No project configuration was weakened to bypass that limitation. The owned static preview stopped after browser testing. The isolated PostgreSQL cluster was stopped after its passing rerun.

Backend result artifacts: `.verification/ntm-frontend-backend/ntm-frontend-backend.trx` and `.verification/ntm-frontend-backend.log`. Frontend transient browser artifacts are ignored under `ntm-greenpulse-frontend/test-results/`; reviewed screenshot artifacts are under `.verification/ntm-ui/`.

## Run and deployment handoff

```powershell
cd ntm-greenpulse-frontend
npm ci
npm start
```

Open **http://localhost:4300**. The development proxy targets the existing local API at `https://localhost:57972`, overridable with a local `NTM_API_TARGET`. No development login credentials are committed. For an approved live environment, an operator must deploy the reviewed EF migrations, securely bootstrap the initial NTM administrator, configure JWT settings and enable `Tenancy:NtmTenantCodes:0=NTM`. Internal access needs the existing explicit grants.

Production assets are `ntm-greenpulse-frontend/dist/ntm-greenpulse-frontend/browser`. Hosting must supply SPA fallback and a same-origin `/api` reverse proxy to the shared API. The static test preview is not a production proxy. Deployment remains a separate action; the existing Es'hlahleni app has not been redeployed.
