See [current workflow and email setup](../docs/REGISTRATION_WORKFLOW.md). This supersedes historical workflow descriptions below.

# iKhethelo Digital API

The admin-only NTM GreenPulse module is documented in [NTM_MODULE.md](../docs/NTM_MODULE.md), with the [verified workbook mapping and source-data exceptions](../docs/NTM_SPREADSHEET_FIELD_MAPPING.md). NTM is disabled by default. Use the reviewed EF migration chain through NtmWorkbookAlignment; the supplied SQL is a reference, not a second deployment script.

The shared ASP.NET Core modular monolith lives entirely in this directory. The governing master, five master documents, OpenAPI and approved SQL remain in the repository's shared directories. Read them before changing behavior. Angular and standalone products such as Practona are outside this backend.

## Build and configure

From the **workspace root** (`ikhethelo-digital`), start hot reload with:

```powershell
dotnet watch --project .\IKhethelo.Digital.Api\src\IKhethelo.Digital.Api\IKhethelo.Digital.Api.csproj run --launch-profile IKhethelo.Digital.Api
```

Open `https://localhost:57972/swagger` manually. The launch profile disables automatic browser launch. Development uses plain console logs so watch can read listening URLs correctly; other environments retain JSON logs. Stop and restart watch after changing launch settings. Commands using `--project src/IKhethelo.Digital.Api` assume you first change into the backend directory below.

Requires .NET SDK 10.0.403 (or a later 10.0.4xx patch) and PostgreSQL. From this directory:

```powershell
dotnet restore
dotnet tool restore
dotnet build --no-restore
```

Configure `ConnectionStrings__DefaultConnection`, `Jwt__Issuer`, `Jwt__Audience`, and `Jwt__PrivateKeyPem` using environment variables or protected configuration. The key must be an RSA private PEM of at least 2048 bits. Never commit it. Development keys can be generated with `RSA.Create(2048).ExportRSAPrivateKeyPem()` in PowerShell 7 or .NET 10 (Windows PowerShell 5.1 does not provide that export method); production keys belong in managed secret storage. JWTs use RS256, a 30-minute expiry and at most 30 seconds clock skew. Rotate keys through an intentional operational rollout; this V1 host loads one configured signing key at startup.

Set `ASPNETCORE_ENVIRONMENT=Development`, configure an approved development origin, then:

```powershell
dotnet ef database update --project src/IKhethelo.Digital.Infrastructure --startup-project src/IKhethelo.Digital.Api
dotnet run --project src/IKhethelo.Digital.Api --no-launch-profile
```

No automatic migration runs at host startup. No applicants or admin passwords are seeded. Use the interactive bootstrap command below to create the first tenant administrator. Subsequent administrator management requires authentication and tenant authorization; public administrator registration is unavailable.

Development Swagger is at `/swagger/index.html`, loading `/swagger/contract.yaml` from the authoritative YAML copied at build/publish time. Use login to obtain a token, then Swagger **Authorize** for protected operations. Authorization is not persisted by the UI. Both Swagger resources are unavailable in Production. Configure HTTPS and approved hostnames/origins before deployment; default AllowedHosts only permits loopback hosts. Production does not allow any CORS origin until configured.

## Visual Studio development configuration

Before the first local launch, run `dotnet run --file scripts/ConfigureDevelopment.cs` from this directory. This saves a generated 2048-bit RSA key and development JWT issuer/audience in .NET User Secrets, outside the repository. Re-running rotates the key and invalidates existing development tokens. The Visual Studio launch profiles select Development, which loads these secrets automatically. Right-click the API project and select **Manage User Secrets** to set `ConnectionStrings:DefaultConnection` to your local PostgreSQL connection string. Never commit secrets or use this development key in production.

## Tests and verification

Integration tests require `IKD_TEST_POSTGRES`, a connection to an **isolated test server** with permission to create databases. They create two randomly named databases: one migrated through EF, one initialized from the SQL baseline. They compare PostgreSQL's canonical column/default/constraint/index/sequence catalogs, exercise the API and drop only those fixture-owned databases afterward. Database tests fail explicitly if the connection is missing; they never silently skip.

```powershell
$env:IKD_TEST_POSTGRES = 'Host=127.0.0.1;Port=<isolated-port>;Database=postgres;Username=<test-role>;Password=<test-secret>'
./scripts/Verify.ps1
```

Do not point tests at production or provide production secrets. Test administrator passwords and RSA keys are generated in memory for each fixture and are not printed. Unit tests can run independently with `dotnet test --filter FullyQualifiedName~ValidationTests`.

`artifacts/initial-migration.sql` describes the original baseline. The additive `AdministrationAndMembershipCategories` migration adds credential versioning and tenant categories; `artifacts/administration-upgrade.sql` is its upgrade script. The normal `__EFMigrationsHistory` table is EF deployment metadata in addition to the six domain tables; it is deliberately excluded from baseline-domain catalog comparison. EF may quote identifiers and reorder DDL; equivalence is verified from PostgreSQL's canonical catalogs, not whitespace. The initial migration's tenant seed is the baseline's code-based UPSERT.

Tenant aliases are controlled server configuration (`Tenancy:Aliases`), with `eshlahleni` -> ESHL. Administrative requests require current active identity, authorized tenant scope and policy checks. Raw locking queries include both resource and tenant IDs. Internal reads are audited before a result is returned; mutation and required audit commit together. Public DTOs cannot choose internal ownership. There is no generic filter bypass or delete endpoint.

Administrative routes also accept the database tenant code, for example `/api/v1/admin/tenants/ESHL/members`. Internal administrators require an explicit grant in trusted configuration: `Tenancy:CrossTenantMembership:<admin-user-uuid>:0 = ESHL` (additional array entries grant additional tenant codes). The default empty map denies all internal membership access; the role alone grants nothing. TenantAdmin access continues to use the account's database tenant assignment. Es'hlahleni's adult requirement and member-number prefix are configured under `Membership:Tenants:ESHL`.

OpenAPI v1.1.0 includes administrator management, direct member registration, categories and history, and documents unknown properties as 400 and canonical administrative tenant codes. Existing membership DTOs remain compatible; category assignments and history have dedicated endpoints. Application details retain the existing DTO, which has no review metadata fields despite the database storing review information.

Only successful login returns the short-lived access token; hashes/passwords are never returned and tokens are never logged. The administration requirements intentionally extend the original contract and schema through OpenAPI v1.1.0 and an additive EF migration.

## Bootstrap the first Es'hlahleni administrator from VS Code

If `dotnet ef database update` reports `42P07: relation "member_number_seq" already exists`, the database may have been installed with baseline SQL without EF migration history. First run this explicit adoption command, and continue only after it reports successful schema verification:

```powershell
dotnet run --project src/IKhethelo.Digital.Api --launch-profile IKhethelo.Digital.Api -- --adopt-existing-database
```

The command verifies the public-schema tables, column types/defaults/nullability, keys/checks/foreign keys, indexes, sequence configuration and domain triggers against the actual migrations. It recognizes the original baseline and the complete administration schema, and records only migrations whose schema is verified. Missing objects, drift or a partial upgrade cause refusal with no history changes. A reference schema is created inside a savepoint and rolled back; no additional database is required. The database role needs schema-creation and migration-history permissions. Existing domain records, password hashes, tenant activation and member-number sequence values are preserved. This command does not apply the administration upgrade or create an administrator; run the migration and bootstrap commands below afterward. Never resolve this error by deleting the sequence/tables or blindly inserting migration-history rows.

Stop the running API with Ctrl+C before building updated code. From the `IKhethelo.Digital.Api` terminal, with PostgreSQL and the protected connection string configured:

```powershell
dotnet tool restore
dotnet ef database update --project src/IKhethelo.Digital.Infrastructure --startup-project src/IKhethelo.Digital.Api
dotnet run --project src/IKhethelo.Digital.Api --launch-profile IKhethelo.Digital.Api -- --bootstrap-admin --tenant ESHL
```

Enter the administrator's email, then a password of 8–128 characters with at least one ASCII digit and one special character (neither a Unicode letter, number nor whitespace), and its confirmation. Password input is not echoed. Bootstrap checks migration readiness before prompting, has no HTTP endpoint, accepts no password argument or redirected input, starts no web server and needs no JWT signing key. Database credentials are loaded from normal protected configuration. Creation and its audit record commit atomically; concurrent/repeated bootstrap refuses to create another account, including when an existing account is inactive. Bootstrap is not an account recovery mechanism. Run it only with trusted operator access to the configured database. Do not rerun the baseline SQL as a schema upgrade; existing EF-managed databases use the additive migration. Baseline-only installations use the verified adoption command above first.

Start normally after setup:

```powershell
dotnet run --project src/IKhethelo.Digital.Api --launch-profile IKhethelo.Digital.Api
```

Open `https://localhost:57972/swagger/index.html`, log in at `/api/v1/auth/login` and use Bearer Authorize. Configure development JWT secrets using `scripts/ConfigureDevelopment.cs` only if not already configured.

## Administrator and member administration

TenantAdmin implicitly selects its assigned tenant at `/api/v1/admin-users`. An internal administrator must supply `?tenantCode=ESHL` and have an explicit, separate `Tenancy:AdminUserManagement:<admin-user-uuid>:0 = ESHL` grant. A membership grant alone does not authorize account management. The existing roles grant TenantAdmin permission to manage other TenantAdmin accounts in the same tenant; no finer administrator role model is introduced.

| Operation | Endpoint | JSON body |
| --- | --- | --- |
| Create administrator | `POST /api/v1/admin-users` | `{"email":"admin@example.com","password":"<strong unique password>","role":"TenantAdmin"}` |
| List/search | `GET /api/v1/admin-users?page=1&pageSize=20&search=admin&isActive=true` | — |
| View | `GET /api/v1/admin-users/{id}` | — |
| Update email/permitted role | `PUT /api/v1/admin-users/{id}` | `{"email":"updated@example.com","role":"TenantAdmin"}` |
| Activate/deactivate | `PATCH /api/v1/admin-users/{id}/status` | `{"isActive":false}` |
| Reset password | `POST /api/v1/admin-users/{id}/reset-password` | `{"newPassword":"<strong unique password>"}` |

Only TenantAdmin may be assigned here: tenant account management cannot grant the internal IKhetheloAdmin role or move ownership. Self-deactivation and deactivating the last active tenant administrator return 409. Email changes, status changes and password resets invalidate the target's existing tokens through a database credential version. Reactivation cannot revive revoked tokens. Password resets are administrator-driven, return no password/token, and are audited without secrets. They do not implement email recovery links or a forced-change-on-next-login flow. Deliver new credentials through your trusted operational channel; the API does not send them.

An authenticated administrator changes their own password with `POST /api/v1/auth/change-password` using `currentPassword`, `newPassword` and `confirmPassword`. It verifies the current password, disallows reuse, revokes existing sessions and records an audit event. The caller must sign in again. Administrator reset is tenant-scoped and cannot target the caller's own account.

Member operations use `/api/v1/admin/tenants/ESHL` (or `eshlahleni`). Existing list/search, profile updates and status changes remain available. New operations:

- `POST /members`: staff in-person completion with the public submission fields plus optional `categoryId`; consent and eligibility remain required. After the R100 step, creates a completed registration snapshot, Active member and audits in one transaction, then attempts the activation email. Existing online registrations must use activation rather than being registered again.
- `GET/POST /membership-categories` and `PUT /membership-categories/{id}`: tenant-defined types/categories, with `{"name":"Standard","isActive":true}` for writes. Names are unique per tenant. Lists support page/pageSize/search/status (Active or Inactive). No arbitrary categories are seeded.
- `GET/PUT /members/{memberId}/category`: view/assign a category using `{"categoryId":"<uuid>"}`; `{"categoryId":null}` clears it. Assign only active categories in the selected tenant. Deactivating a category preserves existing historical assignments.
- `GET /members/{memberId}/history?page=1&pageSize=20`: audit-backed registration, profile-change, category-change and membership-status history with actor/time and available before/after status/category values. Legacy events have no invented before/after data. Raw metadata and contact snapshots are not exposed.

These endpoints provide the administration dashboard's backend; there is no frontend/dashboard project in this workspace.
