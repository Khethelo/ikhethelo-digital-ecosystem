# Es'hlahleni administration implementation

This extends the existing backend for the administration requirements supplied on 5 November 2026. Existing authentication, tenant isolation and membership endpoints remain in place. No dashboard/frontend exists in this workspace; the new functionality is available through the API and Development Swagger.

## Bootstrap

Run from `IKhethelo.Digital.Api` after configuring the protected database connection and applying migrations:

```powershell
dotnet run --project src/IKhethelo.Digital.Api --launch-profile IKhethelo.Digital.Api -- --bootstrap-admin --tenant ESHL
```

The terminal prompts for email and a confirmed, non-echoed password of 12–128 characters. The command does not start the HTTP server and needs no JWT key. No default account/password is seeded. Passwords are not accepted in arguments or redirected input. The active tenant is resolved from the database, its row is locked, and the command refuses to run if any tenant administrator already exists, including inactive accounts. Concurrent bootstrap attempts cannot create multiple initial accounts. Microsoft PasswordHasher creates the hash, and account/audit creation is one transaction. The bootstrap audit identifies the new account and records the trusted-console provisioning method. Successful accounts use the existing login API.

## Administrator management

`AdminUserManagement` implements the new `IAdminUserManagement` application contract. Endpoints are POST/GET `/api/v1/admin-users`, GET/PUT `/api/v1/admin-users/{id}`, PATCH `/api/v1/admin-users/{id}/status`, and POST `/api/v1/admin-users/{id}/reset-password`.

TenantAdmin selects its own tenant automatically and may manage only other TenantAdmin accounts in that tenant. Optional `tenantCode` cannot change that assignment. Internal administrators must explicitly select `?tenantCode=ESHL` and satisfy a separate trusted `Tenancy:AdminUserManagement:<admin-uuid>` list. Existing cross-tenant membership grants do not grant account-management access. Internal successful reads require audits. Unknown/out-of-scope account IDs return 404, and cross-tenant selections are denied.

Only TenantAdmin is a permitted role assignment through these tenant-scoped endpoints. They cannot create internal IKhetheloAdmin accounts or move tenant ownership. The existing role model provides no finer distinctions among TenantAdmins. Details currently mean email and permitted role, matching AdminUser's persisted fields; no invented administrator profile fields were added.

List/search/filter operations are tenant-scoped before counting/pagination. Email identity is normalized and globally unique. Create/update/status/reset operations are audited transactionally. Tenant row locking serializes account-management mutations and prevents concurrent removal of the last active tenant administrator. Self-deactivation is refused. Returned DTOs expose no hashes/passwords, internal tenant IDs or credential versions. All administrator-management responses use no-store.

Password reset is an authenticated administrator-driven replacement, not a public recovery API. New passwords are hashed with Microsoft PasswordHasher. A persisted `security_version` claim invalidates previously issued tokens after resets, email updates and status changes; reactivation cannot restore old tokens. Tokens issued before the feature are treated as version zero and remain subject to database activation/role/tenant/version checks. Reset returns 204 and never logs/audits credentials. Email recovery links, forced first-login password changes and messaging are outside this implementation.

## Member operations and history

`MemberOperations` implements `IMemberOperations` alongside the existing queries/review/administration services:

- Direct registration: POST `/api/v1/admin/tenants/{tenantCode}/members`, accepting submission fields and optional categoryId. Consent/eligibility/contact validation remains authoritative. Registration creates an approved application snapshot, Active member, unique sequence-based member number and required audits in one transaction. Pending/Active identity conflicts return 409. Public submission remains Pending-only and creates no member.
- Categories: GET/POST `/membership-categories` and PUT `/membership-categories/{id}` within the same tenant route. Tenant-defined names are normalized/unique, with active/inactive flags. No arbitrary business categories are seeded. Lists support paging/search/status.
- Assignments: GET/PUT `/members/{memberId}/category`. Only active tenant categories may be assigned; null clears the assignment. A composite database foreign key additionally prevents cross-tenant category assignment. Category updates/assignments are locked and audited. Deactivating a category preserves existing assignments.
- History: GET `/members/{memberId}/history`, paginated and tenant-scoped. Safe projections show actor/time/action and available before/after status/category values, combining member audit events with source approval. Raw metadata and personal contact snapshots are not exposed. Earlier events without change metadata remain visible without fabricated history. Viewing history internally is itself audited, but read audits are excluded from the displayed change history.

Existing member list/detail/update/status DTOs remain compatible. Current category and audit history are separate resources. Existing status values and transitions remain Active/Suspended/Inactive; setting the same status remains a successful no-op. Profile edits remain limited to names/contact fields.

## Persistence and contract changes

The additive migration `20261105154301_AdministrationAndMembershipCategories` adds `admin_users.security_version`, nullable `members.category_id`, and `membership_categories` with a tenant/name unique index and tenant-safe member/category foreign key. Existing domain tables/columns/data are retained. The original migration is unchanged. The baseline SQL now documents the expanded schema; it is a fresh-schema reference, not an upgrade script for existing databases. Upgrade SQL is `artifacts/administration-upgrade.sql`. No existing development/production database has been migrated by this implementation run.

OpenAPI is deliberately extended to v1.1.0, describing all new routes/DTOs. It also reconciles the previously reported unknown-field 400 and canonical administrative tenant-code behavior. PATCH joins the existing JSON size/duplicate/unmapped-member protections and CORS methods. Swagger remains Development-only.

## Files and services

New source files:

- Application/AdministrationContracts.cs
- Infrastructure/Identity/AdminUserManagement.cs (includes BootstrapAdministrator)
- Infrastructure/Membership/MemberOperations.cs
- Api/BootstrapConsole.cs
- Api/Controllers/AdministrationControllers.cs
- Additive migration and generated designer
- Tests/AdministrationTests.cs

Updated entities, EF model/snapshot, DI/JWT validation and issuance, tenant middleware/policies, JSON request middleware, no-store middleware, AuditWriter, member status/approval auditing, test fixture/contract checks, appsettings grant map, SQL baseline, shared OpenAPI, README and verification/development helper scripts. No secrets or default credentials were added.

## Verification

- `dotnet restore`: packages up to date.
- Final `dotnet build --no-restore --configuration Release`: succeeded, zero warnings/errors.
- Full `dotnet test --no-restore --configuration Release --logger 'trx;LogFileName=administration-verification.trx' --results-directory TestResults`: **50 passed, 0 failed, 0 skipped**, duration 48 seconds. This includes all earlier tests and 12 administration tests.
- PostgreSQL 18 verification applied both migrations to a disposable database and the current SQL baseline to another. Exact column/default/constraint/index/sequence catalog parity passed, as did bootstrap/review/deactivation concurrency, tenant isolation, audit-failure rollback, credential revocation, direct registration, categories and history.
- Swagger/OpenAPI validation passed for all 23 route/method combinations and old/new DTO property parity. The live Kestrel Development Swagger/login/Bearer smoke test passed; Production Swagger exclusion passed.
- `dotnet ef migrations has-pending-model-changes ... --configuration Release --no-build`: no pending changes.
- The upgrade script was generated and inspected: forward operations only add the two columns, category table/index/constraints and migration-history entry. It contains no data/table/column deletion.
- A real bootstrap process with redirected input refused to proceed, without requiring a JWT key or attempting account creation. Hashing, successful login, repeat/concurrent refusal and audit rollback were exercised against PostgreSQL through the same bootstrap service. No real administrator account was created for the user.
- The updated verification PowerShell script passed parser validation. Test databases were removed by the fixture; no existing application database was changed.

Initial checks found and fixed administrative path classification and EF alternate-key email-update behavior. Debug outputs were locked by an already running API, so verification used Release without stopping that user process. An intermediate xUnit analyzer error was also corrected. Earlier reports describe previous implementation passes, not this extension. Restart the running API after applying the migration to use the new endpoints.
