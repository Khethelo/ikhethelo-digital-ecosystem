# NTM GreenPulse administrator app

Standalone Angular application for the existing iKhethelo Digital Ecosystem API. Uses standalone components, Bootstrap 5, SCSS, Reactive Forms and typed HTTP services. The Es'hlahleni frontend is a separate application and is not changed or redeployed by this project.

## Run locally

Use the existing repository Node 24 toolchain or an installed compatible Node version:

```powershell
cd ntm-greenpulse-frontend
npm ci
npm start
```

Open `http://localhost:4300`. The development proxy uses `https://localhost:57972` by default. Set `NTM_API_TARGET` to a different **local** backend URL before starting Angular if necessary. Local certificate validation is relaxed only in that development proxy, never a production TLS setting.

`public/config.json` supplies the same-origin API base and canonical tenant code:

```json
{ "apiBaseUrl": "/api/v1", "tenantCode": "NTM" }
```

No secrets or tenant UUIDs belong in this file. Authentication uses the existing `/api/v1/auth/login`. Tokens remain in memory, expire after the backend's 30-minute lifetime and are never put in browser storage. A refresh requires sign-in again. The backend validates tenant membership and grants on every request; configuration and browser claims do not grant permissions.

For a live connection, an operator must first deploy the reviewed backend migrations and enable NTM in the intended approved environment. Initial administrators use the existing secure `--bootstrap-admin --tenant NTM` console; other accounts use the existing authenticated administrator-management endpoints. This frontend does not provide public account registration or apply migrations.

## Implemented screens

- `/login`: administrator sign-in with an authorized NTM API check and safe return navigation.
- `/dashboard`: real network totals, operational counts, recorded funding amount, sector breakdowns and recorded priority needs. Errors hide stale totals rather than manufacturing zero values.
- `/veterans`: server-side search, business-assignment filter and pagination.
- `/veterans/new`, `/veterans/:id`: create/edit veteran identity/contact fields, optional linked business and original membership reference. Either an actual MKMVA or DMV number is required.
- `/businesses`: server-side business-name search, exact sector and status filters, pagination and contextual `veteranId` filtering.
- `/businesses/new`, `/businesses/:id`: business profile, operations, needs/funding and assessment metadata. Available-owner selection is searched and paginated on the server. Veterans with an existing business open that business; racing duplicate assignments show HTTP 409 and keep unsaved form values.
- `/administrators`: tenant-scoped account search/pagination, creation, email editing, activation/deactivation and password resets. Only TenantAdmin is assigned. Backend permissions remain authoritative; internal NTM access does not imply account-management access.

Original membership references and workbook status/employee/years/funding descriptions are displayed separately from validated values. Existing source values are read-only and explicitly retained verbatim in full PUT requests, including null, zero, false and calendar dates. New records can include original source descriptions without inferring typed values. No import/export, veteran accounts, history tables, deletes, notifications or extra workflows are introduced.

## Checks

```powershell
npm run check:contract
npm test
npm run build
npm run test:e2e
```

The contract check reads the repository's actual OpenAPI and verifies DTO fields and mutation whitelists. Unit tests cover API payloads, session behavior, validation, source preservation and assignment races. Browser tests use local synthetic HTTP fixtures, never live customer data. Playwright defaults to installed Chrome on Windows; set `CHROME_PATH` to an installed compatible browser elsewhere. Its owned static preview listens only on `127.0.0.1:4301` and stops after the run.

Production output is `dist/ntm-greenpulse-frontend/browser`. Hosting needs an SPA fallback to `index.html` and a same-origin reverse proxy for `/api` to the shared backend. Do not use the static test preview as a production API proxy. No publishing or production deployment is performed automatically.

Backend authority: `docs/NTM_MODULE.md`, `docs/NTM_SPREADSHEET_FIELD_MAPPING.md`, `docs/NTM_IMPLEMENTATION_REPORT.md`, `contracts/ikhethelo-digital-api.openapi.yaml` and `NtmContracts.cs`. See `docs/NTM_FRONTEND_IMPLEMENTATION_REPORT.md` for verification results.
