# Es'hlahleni frontend implementation report

Implemented at workspace-root `eshlahleni-frontend/`. Angular core **22.2.4**, CLI/build **22.2.6**, TypeScript 6.0.3, Node 24.22.2. The lockfile records exact dependency versions.

## Scope and source of truth

Read the supplied frontend implementation prompt, required master/specification/architecture/data/security/implementation documents, and OpenAPI 1.1.0 before implementation. API request/response DTOs and existing validation were checked against backend source. Angular consumes HTTP contracts, not database tables. The user subsequently authorised clearly marked dummy business content.

The frontend is a separate standalone Angular application with routing, strict compilation, reactive forms, SCSS, local development proxy, lazy feature loading, Vitest and Playwright. No backend endpoint, source, configuration, OpenAPI file or SQL baseline was changed. Hash verification against the pre-implementation snapshots found **zero changed backend/contract/baseline files**. Existing migrations and bootstrap code were invoked only against randomly named disposable acceptance-test databases.

## Delivered screens and files

| Area | Implementation |
| --- | --- |
| Public site | Home, About, Membership, Join, House Rules/FAQ, Contact, draft Membership Terms, draft Privacy Notice and 404 under `src/app/features/public/` |
| Administrator | Login, protected shared layout, overview, applications list/detail/review and members list/detail/edit/status under `features/admin/` |
| HTTP and sessions | `core/api.models.ts`, `api.config.ts`, `membership.services.ts`, `auth.service.ts`, `auth.interceptor.ts`, `auth.guard.ts`, `api-errors.ts` |
| Shared UI | Field validation, safe API errors, loading, status badges, pagination, calendar-date pipe and native confirmation dialog in `shared/ui.ts` |
| Lists and validation | Route-backed server search/filter/pagination in `shared/collection-state.ts`; adult/calendar/contact validation in `shared/validation.ts` |
| Content and SEO | `content/club-content.ts`, `core/seo.ts`, `app.routes.ts`, `index.html`, `public/robots.txt`, sitemap generator |
| Styling | `src/styles/_base.scss`, `_public.scss`, `_admin.scss`; original branded asset in `src/assets/branding/` |
| Developer experience | `Start-Frontend.ps1`, local proxy, npm scripts, VS Code tasks/Chrome launch, README |
| Verification | Unit tests, `e2e/club.spec.ts`, `scripts/check-contract.mjs`, disposable test runner and .NET verification helper |

## Brand and usability

The supplied original JPEG is copied unchanged to `src/assets/branding/eshlahleni-logo.jpeg`. SHA-256: `7A2BF4C3299BC00E96FC503333D1854451D5EC44DAA31372940F3F2C3DC7CBB9`. It appears in the main navigation, hero, club story, footer, login, admin navigation and member card.

The theme uses black/white, charcoal, warm paper, muted olive and bold system typography. It retains the supplied badge, motto and established year. Responsive navigation, stacked mobile forms, table-to-card lists, visible focus, skip links, associated labels, text status badges, native modal confirmations, reduced-motion handling and live error/status feedback are implemented. No external font requests, commerce, product catalogue, invented benefits, fees or categories are present. Desktop/mobile screenshots are in `artifacts/` and use disposable test data.

## Contracts and behaviour

| Action | API integration |
| --- | --- |
| Public application | POST `/api/v1/public/eshlahleni/membership-applications` with exactly seven permitted fields |
| Admin login | POST `/api/v1/auth/login` |
| Search/list/read applications | GET `/api/v1/admin/tenants/ESHL/membership-applications` and `/{applicationId}` |
| Approve/reject | POST application `/{applicationId}/approve` or `/reject`, optional `reviewNotes` omitted when blank |
| Search/list/read members | GET `/api/v1/admin/tenants/ESHL/members` and `/{memberId}` |
| Edit member | PUT member `/{memberId}` with names, email and phone only |
| Change status | PUT member `/{memberId}/status` with Active, Suspended or Inactive |

The public receipt uses the actual application reference and Pending state. Approval displays the actual returned member and ESC number; rejection does not imply membership. Review actions appear only while Pending. A conflict requires reloading before further review. No reviewer identity or review-history text is fabricated because those fields are absent from the application DTO.

The JWT stays solely in service memory, expires after the contractual 900 seconds, and is attached only to configured admin requests. Refreshing the page signs out. Protected 401 responses and expiry clear the session and return to login. There is no persistent browser token storage, refresh-token flow or client-side ownership grant. The frontend uses configured public alias `eshlahleni` and admin code `ESHL`, never an internal tenant UUID.

DOB remains a calendar string, adulthood uses Africa/Johannesburg and .NET-compatible leap-day anniversaries, and displayed timestamps use SAST. Inputs and server field errors follow the existing API validation. Connectivity, unauthorised, forbidden, missing, conflict, validation, oversized, rate-limit and unexpected errors receive safe user-facing explanations.

## Verification results

- Production Angular build passed with strict TypeScript/template checking and configured budgets: approximately **323 kB initial raw / 87 kB estimated transfer**, with lazy feature chunks.
- **12 unit tests passed across 3 files**: calendar dates/adulthood/leap days/Unicode/E.164, exact HTTP payloads/query parameters, review/member status methods, token scope/expiry/401, safe return URLs and safe field/rate-limit/error mapping.
- OpenAPI check passed: submission/update/review fields, statuses, no tenant-ID projection and 900-second lifetime match OpenAPI 1.1.0.
- Real Chrome acceptance testing uses the production build against the actual isolated API and a disposable PostgreSQL database. It performs public registration to Pending, admin login and protected navigation, application search/detail, cancelled then confirmed approval, returned ESC number, member-list lookup, profile update and suspension. A separate public application is rejected and an authenticated API lookup confirms no member exists for it. Browser storage stays empty and refresh requires login again.
- Browser tests also cover mobile navigation/no horizontal overflow, underage blocking, duplicate 409 feedback, 422 field errors, admin pagination, 403/404 feedback and protected 401 expiry redirects.
- Existing Development CORS was verified with a real preflight from `http://localhost:4200`; no configuration edit was needed. The normal local proxy also avoids cross-origin browser calls.
- Each live run removes its own disposable database and stops only its own API process. The club database is not used. Browser traces/video and API logging to test output are disabled.

The first browser passes exposed duplicate form IDs and server-error rendering that unit-level HTTP tests did not catch; both were fixed and checked through the browser workflow.

## Configuration, discrepancies and launch content

No backend/OpenAPI discrepancy requiring a contract change was found. Secondary backend features (administrator management, categories, direct admin member registration, audit/history APIs) are intentionally outside the requested initial frontend workflow.

Development uses same-origin `/api/v1` with a local-only proxy to `https://localhost:57972`; override with `ESHL_API_TARGET`. Production needs HTTPS, `/api/*` reverse proxying, static assets and Angular history fallback. No keys or database credentials belong in frontend environments.

Contact/address/phone and opening hours use replaceable dummy values with standard labels, as requested. House rules, membership terms and privacy wording await club approval. The confirmed production domain is also outstanding; generate the sitemap only after it is supplied. This delivery is client rendered with route metadata; no fabricated canonical origin or published deployment is claimed. The reference site's fetch was unavailable, so design relies on the supplied logo and explicit prompt direction rather than a claimed visual inspection of that site.

Run instructions and test prerequisites are in `README.md`.
