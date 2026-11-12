# API implementation report

Verified on 5 November 2026. All implementation, migrations, tests, configuration, scripts and generated SQL for this run are inside `IKhethelo.Digital.Api/`. The existing backend directory was empty before implementation. No Angular application or authoritative shared contract was changed.

## Implementation

Created a .NET 10 solution with Domain, Application, Infrastructure and API projects, plus a test project. Modules cover Tenancy, Identity, Membership and Audit. Public submission and all administrative routes/DTOs follow OpenAPI v1.0.0. Requests reject unknown/duplicate fields, validate consent and age, and assign ownership server-side. Explicit DTO projections exclude tenant IDs, normalized identities, hashes and audit internals.

Administrative identity uses the approved AdminUser table, Microsoft password hashing, RS256 JWTs, current account/tenant-state validation and policy authorization. Tenant administrators are scoped to their tenant; internal administrators use explicit selected-tenant access with required auditing. Queries, raw locking queries and inserts carry the authorized tenant boundary. Review, member creation/updates and required audit records commit atomically; competing reviews cannot create duplicate members.

Development Swagger serves the actual authoritative YAML copied during build/publish, with Bearer Authorize support. Production Swagger is disabled. CORS uses configured origins, anonymous writes have rate limits and streamed-body limits, and errors/logs avoid raw SQL, credentials, tokens and unnecessary PII. Structured console logging avoids a Windows Event Log privilege dependency.

## Migration and database verification

Created `src/IKhethelo.Digital.Infrastructure/Persistence/Migrations/20261105005230_InitialBaseline.cs`, its generated designer and the EF model snapshot. EF mappings explicitly reproduce the five domain tables, 56 columns, named keys/relationships/checks, all 17 declared indexes including partial unique indexes, timestamp defaults, nullability, enum strings and member_number_seq.

The generated migration was inspected against the approved SQL. Removed EF's unapproved convention-generated reviewer index and disabled implicit FK indexes; the model now declares only approved indexes. Added the baseline's code-based idempotent ESHL tenant UPSERT. No existing/deployed migration history was replaced. Generated deployment/reference SQL is `artifacts/initial-migration.sql`.

Tests applied EF migrations to one newly created PostgreSQL 18 test database and the baseline to another. PostgreSQL's canonical column/default/constraint/index/sequence catalogs matched exactly for domain objects. Checked seed idempotency and absence of pending model changes. Both fixture-owned databases were removed afterward; no existing or production database was migrated.

Approved SQL SHA-256 remains:

```text
1090B5F0331B733868D947B72FB169EFD756D8463D68E1E9DC6A83514534768F
```

## Verification results

- Solution built successfully with no compiler warnings or errors.
- Final `dotnet test --no-restore` run: **36 passed, 0 failed, 0 skipped**.
- `dotnet ef migrations has-pending-model-changes ... --no-build`: no changes since the last migration.
- Formal OpenAPI reader validation: no errors; tested route/method and DTO-property parity against the authoritative document.
- Development Swagger HTML/YAML available; Production Swagger endpoints return 404 over HTTPS.
- Live Kestrel process served Swagger, accepted login and authorized a protected request using the returned Bearer token. Test process was terminated afterward.
- PostgreSQL tests covered tenant isolation, malicious ownership assignment, pending duplicates, active-member conflicts, concurrent approval/rejection, distinct sequence numbers, one member per application, rejection, member updates/status changes and rollback when required auditing fails.
- Security/transport tests covered invalid/expired/forged JWTs, issuer/audience/tenant claims, disabled accounts, unauthorized access, safe receipt/error shapes, 18+ calendar boundaries, consent, dates, malformed JSON, body size, CORS and throttling.

Local detailed results: `TestResults/verification.trx` (ignored generated output). Use `scripts/Verify.ps1` with an isolated `IKD_TEST_POSTGRES` connection to repeat verification. The temporary PostgreSQL cluster was local to `.work/` on loopback port 55439; it is not deployment configuration.

Initial verification exposed Windows Event Log privilege dependency, missing validation-error fields and an overly specific CHECK-order assertion; these were corrected. Test connection pooling and cold-start readiness handling were adjusted for the local verification environment. The final full run passed after these corrections; no tests were skipped or weakened to accept application failures.

## Deliberate interpretations and expected differences

The API prompt's blanket statement that tokens must never be returned conflicts with OpenAPI's required login access-token response. This was reported before implementation: only successful login returns a short-lived token; passwords/hashes are never returned and tokens are never logged. No authoritative contract was edited.

The master's contract-pass-only instructions concern the preceding master pass; the user's present API work order explicitly authorizes implementation, migrations and PostgreSQL verification. The SQL comment about a possible full Identity switch does not authorize such a switch in V1. Only the fixed AdminUser identity schema is implemented.

EF adds its normal `__EFMigrationsHistory` deployment metadata table; it is not an additional domain entity and is excluded from the domain-schema comparison. Generated SQL may quote/reorder DDL and omit sequence clauses whose PostgreSQL defaults are equivalent. Actual database catalogs, including sequence cache and limits, were compared rather than requiring byte-identical SQL formatting. No unexplained persistence drift remains.

## Running and operational scope

See README.md for build, configuration and startup commands. Running outside tests requires a configured database connection, JWT issuer/audience/private key, deployment hosts/origins and a securely provisioned administrator. No production credentials or applicant data are seeded. Production deployment, account provisioning, TLS/proxy configuration and the Angular frontend remain outside this implementation run. No unresolved implementation or contract conflict remains.
