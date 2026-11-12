# Existing PostgreSQL baseline adoption

The duplicate `member_number_seq` error occurs when EF tries to apply its initial migration to a schema whose objects already exist but whose initial migration history is absent. A failed `dotnet ef database update` can leave an empty history table. An SQL-installed database can have no history table at all. Bootstrap cannot use the administration model until the schema upgrade is applied.

The explicit `--adopt-existing-database` operator command verifies the public domain schema against reference schemas generated from the actual migrations. It recognizes either the original baseline or the complete administration schema, and records only verified migrations. It rejects missing/changed definitions, partial upgrades, unexpected domain triggers, incompatible history and empty databases. Unknown history is not silently overwritten. Reference objects are built in a transaction savepoint and rolled back; no domain data or sequence values are changed. Original migrations are unchanged. No application database is automatically migrated or adopted at host startup.

From `IKhethelo.Digital.Api`, run each command separately and continue only after success:

```powershell
dotnet run --project src/IKhethelo.Digital.Api --launch-profile IKhethelo.Digital.Api -- --adopt-existing-database
dotnet ef database update --project src/IKhethelo.Digital.Infrastructure --startup-project src/IKhethelo.Digital.Api
dotnet run --project src/IKhethelo.Digital.Api --launch-profile IKhethelo.Digital.Api -- --bootstrap-admin --tenant ESHL
```

The command loads the existing protected connection configuration. It requires CREATE-schema and migration-history permissions. Bootstrap now checks pending migrations before requesting credentials and gives setup guidance if migrations are incomplete. Do not delete existing domain objects or blindly insert migration-history rows.

## Verification in this repair pass

- Release solution build succeeded with zero warnings/errors; subsequent final build includes the corrected PostgreSQL catalog casts and missing-history handling.
- EF pending-model check passed; no schema/model/migration changes were required.
- Direct CLI verification against three freshly created disposable PostgreSQL databases passed: original baseline with an empty history table, original baseline with no history table, complete SQL baseline with no history table, repeated adoption, and additive EF upgrade without duplicate-object errors.
- Queries confirmed preservation of tenant name, administrator hash/version, Pending application and sequence value 420, and verified no reference schema remained.
- Removing a required index caused explicit refusal and left history empty; existing account data and reference-schema cleanup were verified. Empty database refusal created no public tables.
- Five PostgreSQL regression cases were added in `DatabaseAdoptionTests.cs`, including data/member preservation, bootstrap/login after adoption, partial-upgrade rejection and empty-database refusal. Windows Application Control blocked loading the test assembly, even after an approved rebuild outside the sandbox; these automated cases were **not executed**. The successful checks above used the real application CLI and PostgreSQL queries instead. No Windows policy was disabled or changed.
- During direct checks, a PostgreSQL internal-char concatenation ambiguity and Npgsql's missing-history SELECT inside a transaction were found and fixed before the successful final checks.

Only disposable verification databases were modified. The user's configured application database and administrator accounts were not changed by this repair pass.
