# NTM GreenPulse implementation report — 13 November 2026

The admin-only backend is implemented and reconciled with the supplied workbook and SQL. All 20 source columns are documented in [NTM_SPREADSHEET_FIELD_MAPPING.md](NTM_SPREADSHEET_FIELD_MAPPING.md). NTM remains disabled by default; no workbook data or existing application database was changed. Migrations are exercised only against disposable databases on a newly initialized, loopback-only PostgreSQL test cluster.

## Source reconciliation

The workbook contains employee qualifiers, descriptive years/funding values, status typos, generic DMV membership markers and malformed contact information. The final model preserves those values instead of guessing corrections. It adds owner email, original membership reference, workbook record number and four source-text fields alongside validated status/count/years/funding values. Unknown source values do not manufacture dashboard counts.

The supplied SQL is preserved as `database/ntm-greenpulse-supplied-reference.sql`, not executed. Status length, numeric precision and name/assessment-date indexes match that reference. Generated normalized registration columns retain equivalent case-insensitive uniqueness to its expression indexes. Timestamp triggers are omitted to follow the existing TimeProvider/explicit-write/audit convention. Original baseline SQL and historical migrations remain unchanged.

Generic DMV markers do not satisfy the confirmed requirement for an actual MKMVA or DMV number. Those records require identity reconciliation before creation. Duplicate veteran/business source rows must respect the confirmed one-business rule. No import/export or fabricated identifier is introduced to bypass these checks.

## Schema, migration and routes

[NTM_MODULE.md](NTM_MODULE.md) contains the architecture review, complete column definitions, relationships, authorization and operational instructions.

`NtmVeteran -> ntm_veterans` has server-generated UUID, tenant ownership, full name, nullable MKMVA/DMV/contact/email/reference and UTC timestamps. Either actual registration identifier suffices. Generated normalized columns and tenant-scoped partial unique indexes enforce independent MKMVA/DMV uniqueness.

`NtmBusiness -> ntm_businesses` has server-generated UUID, tenant ownership, required veteran, business identity/descriptive/assessment fields, nullable validated operational/count/years/funding fields, source-preserving text, workbook record number and UTC timestamps. A unique tenant/veteran index plus restricted composite FK enforce zero-or-one business and same-tenant ownership. Years use numeric(6,1), Rand funding numeric(15,2); application validation rejects excess precision/range rather than rounding.

Migrations:

- `20261112205247_NtmGreenPulseProvisional`: original additive NTM schema and generated-UUID tenant provisioning by code, retained intact.
- `20261112210948_NtmWorkbookAlignment`: verified-source fields, precision and indexes. An exclusive table lock and preflight check refuse loss of existing draft numeric values before ALTER TYPE. Correction requires an explicit operator action, never silent rounding/deletion.

Current review SQL: `IKhethelo.Digital.Api/artifacts/ntm-workbook-upgrade.sql`, covering both NTM migrations after AdministrationAndMembershipCategories. The older `ntm-provisional-upgrade.sql` documents the initial draft only; use the current complete chain. Existing database adoption verifies each of the four schema versions before recording corresponding history, refusing drift and unsupported history.

All nine operations require authenticated NtmAccess under `/api/v1/admin/tenants/{tenantCode}/ntm`:

| Methods | Suffix |
| --- | --- |
| GET | `/dashboard` |
| GET, POST | `/veterans` |
| GET, PUT | `/veterans/{id}` |
| GET, POST | `/businesses` |
| GET, PUT | `/businesses/{id}` |

Lists support stable pagination/search/filtering. Dashboard aggregates are real tenant-scoped values from one consistent snapshot. PostgreSQL unique violations return generic 409, including concurrent assignments. Writes and required audit records commit atomically. Raw personal/source snapshots are not audited. No delete, public registration, veteran authentication, assessment history or extra workflows are added.

Authorization reuses existing active/versioned JWT identities and tenant middleware. NtmAccess additionally requires `Tenancy:NtmTenantCodes` enablement, empty by default. TenantAdmin must match the selected tenant. Internal access requires separate explicit CrossTenantNtm grants and uses existing internal-read auditing. Request bodies cannot set tenant ownership. Existing secure bootstrap, admin management and their separate grants remain authoritative.

## Files created

Under `IKhethelo.Digital.Api/src/`:

- `IKhethelo.Digital.Domain/NtmEntities.cs`
- `IKhethelo.Digital.Application/NtmContracts.cs`
- `IKhethelo.Digital.Infrastructure/Persistence/NtmConfiguration.cs`
- `IKhethelo.Digital.Infrastructure/Ntm/NtmOperations.cs`
- `IKhethelo.Digital.Infrastructure/Persistence/Migrations/20261112205247_NtmGreenPulseProvisional.cs` and its `.Designer.cs`
- `IKhethelo.Digital.Infrastructure/Persistence/Migrations/20261112210948_NtmWorkbookAlignment.cs` and its `.Designer.cs`
- `IKhethelo.Digital.Api/Controllers/NtmController.cs`

Other additions:

- `IKhethelo.Digital.Api/artifacts/ntm-provisional-upgrade.sql`
- `IKhethelo.Digital.Api/artifacts/ntm-workbook-upgrade.sql`
- `IKhethelo.Digital.Api/tests/IKhethelo.Digital.Tests/NtmValidationTests.cs`
- `IKhethelo.Digital.Api/tests/IKhethelo.Digital.Tests/NtmIntegrationTests.cs`
- `IKhethelo.Digital.Api/tests/IKhethelo.Digital.Tests/NtmContractTests.cs`
- `database/ntm-greenpulse-supplied-reference.sql`
- `docs/NTM_MODULE.md`, `docs/NTM_SPREADSHEET_FIELD_MAPPING.md`, `docs/NTM_IMPLEMENTATION_REPORT.md`

## Files modified

Under `IKhethelo.Digital.Api/`:

- `src/IKhethelo.Digital.Api/Program.cs`: DI/policy registration.
- `src/IKhethelo.Digital.Api/Tenancy/TenantScopeMiddleware.cs`: NTM enablement/grant configuration and handler.
- `src/IKhethelo.Digital.Infrastructure/Persistence/DigitalDbContext.cs`: shared DbSets/configurations.
- `src/IKhethelo.Digital.Infrastructure/Persistence/ExistingDatabaseAdoption.cs`: four-version catalog verification.
- `src/IKhethelo.Digital.Infrastructure/Persistence/Migrations/DigitalDbContextModelSnapshot.cs`: generated current snapshot.
- `tests/IKhethelo.Digital.Tests/ApiFixture.cs`: isolated test configuration, NTM grants and additive baseline comparison.
- `tests/IKhethelo.Digital.Tests/ContractAndPersistenceTests.cs`: new contract/schema expectations.
- `tests/IKhethelo.Digital.Tests/DatabaseAdoptionTests.cs`: older/current schema adoption and numeric-loss refusal coverage.
- `tests/IKhethelo.Digital.Tests/KestrelSmokeTests.cs`: disable external email explicitly in the isolated child host.
- `README.md`: current module/mapping/deployment references.

Shared updates: `contracts/ikhethelo-digital-api.openapi.yaml` (v1.2.0), `docs/DATA_MODEL.md` (additive NTM reference).

## Verification

The final backend PostgreSQL rerun is complete. On 13 November 2026, `dotnet build --no-restore -c Release` passed with zero warnings and errors; `dotnet test --no-build --no-restore -c Release` passed **85 tests, 0 failed, 0 skipped**, including the live Kestrel smoke test and existing Es'hlahleni regressions. Duration: 1 minute 59 seconds. The saved result is `.verification/ntm-frontend-backend/ntm-frontend-backend.trx`, with console output in `.verification/ntm-frontend-backend.log` (ignored local verification artifacts).

This fresh run supersedes the earlier pending/failed verification attempts. It used only the dedicated loopback PostgreSQL cluster and randomly named fixture databases. No production migration, live application database update, or workbook import was performed. Existing SQL, historical migrations and Es'hlahleni frontend files were not changed by the separate NTM frontend implementation.

Coverage includes registration rules, source-field round trips, database FK/uniqueness, concurrency, tenant isolation, numeric validation, real dashboard aggregates, audit rollback, schema catalogs/adoption, OpenAPI contracts and existing Es'hlahleni/admin/authentication/membership/email behavior. No EF in-memory database is used.

## Standalone Angular frontend

The complete administrator frontend now lives in `ntm-greenpulse-frontend/`, independently of Es'hlahleni. See [NTM_FRONTEND_IMPLEMENTATION_REPORT.md](NTM_FRONTEND_IMPLEMENTATION_REPORT.md) and its README for screens, verification and local run instructions. It uses the authoritative backend DTOs and endpoints; deployment remains a separate operator action.

## Deployment and outstanding data work

1. Reconcile ambiguous workbook records and actual MKMVA/DMV identities before any future import. Structural mapping is verified; source-record corrections have not been invented or applied.
2. Back up, review the generated complete upgrade SQL, and stage the migration through the existing EF deployment process. From `IKhethelo.Digital.Api`, run `dotnet ef database update --project src/IKhethelo.Digital.Infrastructure --startup-project src/IKhethelo.Digital.Api` only against the intended approved environment. Baseline-only installations require verified adoption first. Never execute equivalent reference SQL as a second schema installation.
3. Use existing interactive `--bootstrap-admin --tenant NTM` with protected configuration for the initial administrator; authenticated `/api/v1/admin-users` manages the others. No passwords belong in arguments/source control. Provisioning uses tenant code, generated UUIDs and existing tables.
4. Enable `Tenancy:NtmTenantCodes:0=NTM` after schema deployment. Configure protected JWT settings and separate internal NTM/account-management grants only where needed; role or membership access alone does not grant internal NTM access.
5. Verify authorized access and existing membership behavior in staging before production rollout. No production migration, live tenant/admin provisioning or workbook import was performed in this session.
