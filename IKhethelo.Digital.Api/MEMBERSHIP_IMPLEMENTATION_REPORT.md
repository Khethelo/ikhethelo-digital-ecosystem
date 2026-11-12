# Es'hlahleni membership implementation — 5 November 2026

## Scope and implementation

The repository already contained working implementations of the five requested interfaces. This pass inspected the PostgreSQL baseline in full, entities/mappings, controllers, DTOs, middleware, dependency injection, identity, tenant policies, membership services and tests, and corrected gaps against the supplied membership implementation prompt. Existing functionality was preserved rather than recreated.

- `SubmitMembershipApplication` stores a tenant-owned Pending application, trimmed names/contact normalization, UTC timestamps and no member. The configured public alias resolves to an active database tenant. Consent, canonical E.164, field lengths and configured adult eligibility are validated.
- `AdministratorAuthentication` uses Microsoft `PasswordHasher<AdminUser>`, current active account/tenant checks, generic credential errors, password rehashing and a required login audit. RS256 JWTs validate issuer, audience, algorithm, expiry and current database identity. Tokens expire after 15 minutes. Authentication responses now carry `Cache-Control: no-store` even when transport validation or throttling prevents controller execution.
- `MembershipQueries` applies tenant filtering before search/count/pagination, with deterministic timestamp/UUID ordering. Guessed out-of-scope identifiers return 404. Internal successful reads require persisted audit records.
- `ReviewApplications` locks the tenant-scoped application using parameterized PostgreSQL `SELECT ... FOR UPDATE` inside a transaction. Only Pending may transition. Approval allocates the existing sequence, creates one member and records reviewer/time/audit before committing. Rejection records review/audit without creating a member. Competing reviews serialize; the loser receives 409. The existing application/member unique constraint provides additional protection.
- `MemberAdministration` locks tenant-scoped rows for replacement of allowed names/contact fields and status changes. Identity, ownership, application link, birth date and join timestamp are preserved. Active contact conflicts return 409, including racing unique violations. The existing contract permits transitions among all three declared statuses and treats setting the current status as a successful no-op. Mutations and audits commit together.
- `TenantData` is the existing scoped query boundary; EF Core/Npgsql `DigitalDbContext` remains persistence. No alternative repositories/controllers/interfaces or schema changes were introduced.

## Changes in this pass

Created:

- `src/IKhethelo.Digital.Infrastructure/Tenancy/MembershipRules.cs`: trusted per-tenant eligibility and number-prefix configuration, removing ESHL conditionals from submission/review services.
- This report.

Modified:

- `src/IKhethelo.Digital.Api/Tenancy/TenantScopeMiddleware.cs`: administrative canonical tenant codes, explicit per-administrator/per-tenant grants, and evaluation of `CrossTenantMembership` from `MembershipAccess` for internal administrators. Default configuration grants no internal access. Active identity validation still happens on every authenticated request.
- `src/IKhethelo.Digital.Api/Http/RequestBodyMiddleware.cs`: unknown/duplicate properties, including injected tenant IDs, return 400. Existing 16 KiB checks remain.
- `src/IKhethelo.Digital.Api/Http/Problems.cs`: authentication no-store header before downstream processing.
- `src/IKhethelo.Digital.Api/Program.cs`: register configured membership rules.
- `src/IKhethelo.Digital.Api/appsettings.json`: remove stored database credentials, configure ESHL rules and deny internal access by default.
- `src/IKhethelo.Digital.Infrastructure/Membership/SubmitMembershipApplication.cs` and `ReviewApplications.cs`: consume configured membership rules.
- `tests/IKhethelo.Digital.Tests/ApiFixture.cs` and `IntegrationTests.cs`: explicit internal test grants, canonical-code workflow, deny-by-default cross-tenant access, no member upon submission, HTTP 400 ownership/unknown-field rejection and failed-login no-store checks.
- `README.md`: configuration and contract differences.

## Database and audit

Existing EF Core/Npgsql mappings, initial migration and baseline are unchanged. The five domain tables remain tenants, admin_users, membership_applications, members and audit_logs; EF deployment metadata is separate. No destructive migration, production database operation or new status/column was introduced. The SQL baseline SHA-256 remains `1090B5F0331B733868D947B72FB169EFD756D8463D68E1E9DC6A83514534768F`.

Audits record actor, selected tenant, action, resource, UTC timestamp, correlation and internal-access metadata. Member status responsibility/timestamp are represented by the transactional audit and member update timestamp because the baseline has no separate status-changed-by/status-changed-at columns.

## Contract differences and operational requirements

The supplied request requires unknown fields to return 400 and demonstrates administrative `ESHL` routes. Shared OpenAPI v1.0.0 documents unknown-field failures under 422 and a lowercase alias pattern. Runtime follows the current request; the shared document is unchanged and therefore still describes these two cases differently. Public requests continue to require configured aliases. DTO names/shapes/routes are preserved. The application response DTO has no review metadata properties; review information persists in the database but is not added to the response contract.

Secure initial administrator provisioning remains an operational prerequisite: there is no existing provisioning command or public registration endpoint. A trusted operator must create the baseline-compatible account with Microsoft password hashing and required security auditing; no default password/account is created.

Set `ConnectionStrings:DefaultConnection` using User Secrets or protected environment configuration. Configure `Jwt:Issuer`, `Jwt:Audience` and a managed RSA private PEM (at least 2048 bits). For local development the existing `dotnet run --file scripts/ConfigureDevelopment.cs` writes development JWT secrets. Use the existing Visual Studio project/IIS Express Development launch profile. Apply migrations only to an intended fresh database through the established deployment process; an already baseline-initialized database needs intentional EF migration-history adoption rather than rerunning initial table creation.

For internal access, configure `Tenancy:CrossTenantMembership:<admin-user-uuid>:0` as `ESHL`, with additional numbered entries only for specifically authorized tenants. Tenant administrators use their database assignment and need no cross-tenant grant. Tenant aliases remain `Tenancy:Aliases`; ESHL rules live at `Membership:Tenants:ESHL`.

Open `/swagger/index.html` in Development, submit a public application, call `/api/v1/auth/login`, copy `accessToken` into Bearer Authorize, then list/review applications and manage members. Use `eshlahleni` in Swagger's existing alias parameter examples, or `ESHL` for administrative HTTP calls. Production Swagger remains disabled. Actual Visual Studio interaction is not automated by this pass.

## Fresh verification

- `dotnet restore`: passed; packages up to date.
- `dotnet build --no-restore`: passed with zero warnings/errors. The subsequent full test command also rebuilt all projects successfully.
- `dotnet test --no-restore --logger 'trx;LogFileName=membership-verification.trx' --results-directory TestResults`: **38 passed, 0 failed, 0 skipped**, duration 2 minutes 26 seconds. Results: `TestResults/membership-verification.trx`.
- Real PostgreSQL 18 on the existing isolated loopback cluster at port 55439: disposable EF-migrated and baseline-initialized databases were created by the fixture. Tests verified exact database catalog equivalence, tenant seeding, constraints, review races, distinct sequence allocation, required-audit rollback, authentication/authorization, transport limits and member management. The fixture removed its test databases afterward.
- `dotnet ef migrations has-pending-model-changes --project src/IKhethelo.Digital.Infrastructure --startup-project src/IKhethelo.Digital.Api --no-build`: passed, no pending model changes.
- Automated Swagger checks verified valid shared OpenAPI, all ten route/method combinations and DTO-property parity, Development availability, Production exclusion and Bearer authentication. The live Kestrel smoke test passed. Semantic documentation differences described above remain even though structural parity passes.

The earlier `IMPLEMENTATION_REPORT.md` describes an earlier run; the results above were executed in this pass. One intermediate test invocation encountered a compile error while the new rules class was being added; the completed build and full rerun passed. No production data was read or changed, and no database password/private key was added to configuration files.
