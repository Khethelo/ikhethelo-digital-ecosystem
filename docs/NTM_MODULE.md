# NTM GreenPulse backend

The admin-only module extends the existing .NET 10 ASP.NET Core application under `IKhethelo.Digital.Api/src`. It uses the existing PostgreSQL database, DigitalDbContext, JWT identity, tenant authorization, audit logs and administrator management. Veterans have no accounts. There are no public NTM routes, assessment history tables, workflow approvals, notifications, funding disbursements or import/export endpoints.

The [workbook mapping](NTM_SPREADSHEET_FIELD_MAPPING.md) now verifies all 20 source headings, types and transformations against the supplied workbook and SQL. It records unresolved source-data defects explicitly. Schema verification does not authorize importing ambiguous rows or automatically deploying to production. NTM stays disabled by default until an operator enables its tenant.

## Architecture review

`IKhethelo.Digital.Api/IKhethelo.Digital.slnx` contains Domain entities, Application DTOs/interfaces/validation, Infrastructure EF-backed services and the API MVC host. Infrastructure services query EF directly, following the existing pattern rather than adding a generic repository. One DigitalDbContext owns all tables. Explicit snake-case mappings and named constraints follow the existing configuration style; implicit FK indexes remain disabled.

Existing `InitialBaseline` and `AdministrationAndMembershipCategories` migrations, original baseline SQL and Es'hlahleni behavior remain intact. The first NTM migration establishes the initial two-table schema; the subsequent workbook-alignment migration adds source-preserving fields and SQL precision/indexes. Existing database adoption verifies each of the four supported schema versions before recording corresponding history, refusing drift or unsupported history. It does not stamp missing NTM migrations onto an older database.

JWT validation rechecks active administrator, role, assigned active tenant and credential version. TenantScopeMiddleware resolves the active URL tenant and prevents TenantAdmin cross-tenant selection. NtmAccess follows the existing authorization-handler pattern but requires explicit tenant enablement and separate internal NTM grants. Request DTOs never contain tenant IDs; unknown JSON properties are rejected by the existing pipeline. Every service query starts with the resolved tenant ID. Internal reads use the existing CrossTenantRead audit mechanism.

## Entity and physical schema

Both tables use UUID `id` PKs generated with Guid.NewGuid, required UUID `tenant_id` FKs to tenants, and required UTC timestamptz created_at_utc/updated_at_utc. IDs have no guessed tenant UUID or database UUID entity default. Application writes set timestamps through TimeProvider, matching current conventions; no timestamp triggers are added.

`NtmVeteran -> ntm_veterans`:

| Fields | Storage |
| --- | --- |
| FullName | Required varchar(200), nonblank |
| MkmvaNumber, DmvNumber | Nullable varchar(100); at least one actual registration required |
| ContactNumber | Nullable varchar(50), recorded source text |
| EmailAddress | Nullable varchar(320), recorded source text; never an authentication/outbound-email field |
| MembershipReference | Nullable varchar(100), original combined workbook reference; nonunique |
| NormalizedMkmvaNumber, NormalizedDmvNumber | Stored generated varchar(100): nullif(upper(btrim(number)), '') |

Independent partial unique indexes on `(tenant_id, normalized_mkmva_number)` and `(tenant_id, normalized_dmv_number)` protect case-insensitive registration uniqueness, including concurrent/direct SQL writes. Alternate key `(tenant_id,id)` supports tenant-safe FK ownership. A `(tenant_id,full_name)` index supports lists. A reference such as DMV Database is retained separately and must not be fabricated into a registration number.

`NtmBusiness -> ntm_businesses`:

| Fields | Storage |
| --- | --- |
| VeteranId | Required UUID, composite FK with tenant_id |
| BusinessName | Required varchar(200), nonblank |
| CipcRegistrationNumber | Nullable varchar(100) |
| BusinessAddress, BusinessBackground, CurrentBusinessNeeds, PriorityNeed, AdditionalComments | Nullable text |
| Sector, AssessedBy | Nullable varchar(200) |
| OperationalStatus | Nullable varchar(30), three declared states |
| NumberOfEmployees | Nullable nonnegative integer |
| YearsInOperation | Nullable nonnegative numeric(6,1) |
| FundingRequired | Nullable boolean |
| FundingAmount | Nullable nonnegative numeric(15,2), South African Rand |
| AssessmentDate | Nullable date |
| SourceRecordNumber | Nullable positive integer; workbook tracker, not an identity key |
| OperationalStatusSource, NumberOfEmployeesSource, YearsInOperationSource, FundingRequiredSource | Nullable text, retained verbatim independently of validated fields |

The required composite FK `(tenant_id,veteran_id) -> ntm_veterans(tenant_id,id)` prevents cross-tenant assignment. A unique tenant/veteran index enforces at most one business per veteran. All tenant/veteran deletes are restricted. Navigations are NtmVeteran.Business (optional) and NtmBusiness.Veteran (required). No delete/deactivate endpoints are added. PUT can reference another available same-tenant veteran, subject to the same constraint.

Indexes also cover `(tenant_id,business_name)`, `(tenant_id,sector)`, `(tenant_id,operational_status)`, `(tenant_id,assessment_date)`. Numeric validation rejects excess precision/range rather than silently rounding. The alignment migration refuses existing draft values that cannot fit the supplied SQL precision without loss. Source descriptions do not set typed values automatically. The three validated statuses are Operational, Partially Operational, Not Operational, as specified by workbook Instructions.

## API

Base: `/api/v1/admin/tenants/{tenantCode}/ntm`. All operations require authenticated NtmAccess.

| Method | Suffix | Behavior |
| --- | --- | --- |
| GET | `/dashboard` | Real aggregate snapshot |
| GET / POST | `/veterans` | List or create |
| GET / PUT | `/veterans/{id}` | Detail with optional business summary or replace |
| GET / POST | `/businesses` | List or create |
| GET / PUT | `/businesses/{id}` | Detail or replace |

Lists accept page (default 1), pageSize (default 20, maximum 100), search. Veterans search name and registration numbers and accept hasBusiness. Businesses search business name and accept sector, operationalStatus, veteranId. Counts/filters are tenant-scoped and ordering is stable by name and UUID. OpenAPI v1.2.0 documents the routes and DTOs, including source-preserving fields.

Creates return 201/Location; updates/reads 200. Missing/out-of-tenant resources and foreign-tenant assignments return 404. Unauthorized tenant selection returns 403, unauthenticated requests 401, field/business errors 422, malformed/unknown properties 400. Duplicate registration/assignment PostgreSQL unique violations return generic 409, including races. Existing 16 KiB JSON body cap remains. NTM responses use no-store caching.

Dashboard reports total veterans/businesses, each validated status count, sum of recorded funding amounts, businesses explicitly requiring funding, sector counts and whole recorded priority-need counts. Amount sums all recorded amounts independently of the nullable funding flag. Unknown/raw statuses and funding descriptions do not manufacture classified counts; null sectors/needs are omitted. Repeatable-read provides one committed snapshot. Personal details and raw source values are not included.

NtmVeteranCreated/Updated and NtmBusinessCreated/Updated audits include the existing tenant, actor, action, entity, correlation and UTC fields, without contact/registration/source snapshots. Entity and audit commit in the same SaveChanges transaction. Existing authentication/account-management audits remain authoritative.

## Provisioning and deployment

1. Review the workbook mapping and resolve source-data exceptions before any future import. The supplied reference SQL is preserved for comparison only; do not execute it and equivalent EF migrations against the same database.
2. Review `artifacts/ntm-workbook-upgrade.sql`, covering the initial NTM schema plus `20261112210948_NtmWorkbookAlignment`. Historical migrations are preserved. Back up and stage the approved upgrade; it is not applied at application startup.
3. For baseline-only installations lacking EF history, use existing `--adopt-existing-database` first. Adoption verifies catalogs and refuses drift. From `IKhethelo.Digital.Api`, apply reviewed migrations using `dotnet ef database update --project src/IKhethelo.Digital.Infrastructure --startup-project src/IKhethelo.Digital.Api`. Never insert/delete history manually or rerun baseline SQL as an upgrade.
4. The original NTM seed resolves `NTM` by code and creates a UUID only when absent; it preserves existing tenant IDs and activation state. No passwords or business data are seeded. Run existing interactive `dotnet run --project src/IKhethelo.Digital.Api -- --bootstrap-admin --tenant NTM` with protected connection configuration. Passwords cannot be provided through arguments or redirected input; bootstrap hashes and audits atomically, and refuses repeat/concurrent setup.
5. Configure existing protected JWT settings for normal hosting, then enable `Tenancy:NtmTenantCodes:0=NTM` (environment `Tenancy__NtmTenantCodes__0=NTM`). Canonical NTM routing needs no public alias or membership rules.
6. TenantAdmin uses existing `/api/v1/admin-users` to manage the other administrators. Internal account managers separately need `Tenancy:AdminUserManagement:<actor-uuid>:0=NTM`; internal NTM readers/writers need `Tenancy:CrossTenantNtm:<actor-uuid>:0=NTM`. A membership/account-management grant alone does not grant NTM access.

## Tests

The existing xUnit framework and PostgreSQL fixtures are extended, without EF's in-memory provider. Unit/model/contract tests verify identifiers, fields, precision, relationships and OpenAPI DTOs. PostgreSQL tests cover duplicate races, tenant isolation, real dashboard aggregates, source-field round trips, database constraints, atomic audits and adoption. Existing Es'hlahleni, authentication, administration, membership and email tests remain regression coverage.

Configure `IKD_TEST_POSTGRES` for an isolated server with CREATE DATABASE permission, then `dotnet test -c Release`. Fixtures create/drop only randomly named disposable databases. Never use a production server. Release avoids locked Debug assemblies if a development host is running. See [implementation report](NTM_IMPLEMENTATION_REPORT.md) for actual results and remaining deployment work.
