using IKhethelo.Digital.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace IKhethelo.Digital.Infrastructure.Persistence;

// Explicit operator action for databases originally installed from the SQL baseline.
// Never suppress a duplicate-object error or stamp migration history without checking the schema.
public sealed class ExistingDatabaseAdoption(DigitalDbContext db)
{
    public const string InitialMigration = "20261105005230_InitialBaseline";
    public const string AdministrationMigration = "20261105154301_AdministrationAndMembershipCategories";
    public const string NtmMigration = "20261112205247_NtmGreenPulseProvisional";
    public const string NtmWorkbookMigration = "20261112210948_NtmWorkbookAlignment";
    public const string TenantStaffMigration = "20261119231544_TenantStaffRoles";
    private static readonly string[] Tables = ["tenants", "admin_users", "membership_applications", "members", "audit_logs", "membership_categories", "ntm_veterans", "ntm_businesses"];

    public async Task<string> ExecuteAsync(CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var pgTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        async Task Run(string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection, pgTransaction);
            await command.ExecuteNonQueryAsync(ct);
        }
        await Run("SELECT pg_advisory_xact_lock(72146837); SET LOCAL search_path = public, pg_catalog;");
        // Npgsql's optimistic history read handles a missing table by catching 42P01.
        // Inside our explicit transaction that would leave PostgreSQL aborted, so
        // check existence before issuing the history SELECT.
        var applied = new List<string>();
        await using (var exists = new NpgsqlCommand("SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL", connection, pgTransaction))
        {
            if ((bool)(await exists.ExecuteScalarAsync(ct))!)
            {
                await using var history = new NpgsqlCommand("SELECT \"MigrationId\" FROM public.\"__EFMigrationsHistory\"", connection, pgTransaction);
                await using var rows = await history.ExecuteReaderAsync(ct);
                while (await rows.ReadAsync(ct)) applied.Add(rows.GetString(0));
            }
        }
        if (applied.Except([InitialMigration, AdministrationMigration, NtmMigration, NtmWorkbookMigration, TenantStaffMigration], StringComparer.Ordinal).Any())
            throw new RequestFailure(409, "Unrecognized migration history. Adoption refused; the database was not changed.");

        // Lock only existing, known domain tables. This keeps their schema stable during verification.
        foreach (var table in Tables)
        {
            await using var exists = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_tables WHERE schemaname='public' AND tablename=@name)", connection, pgTransaction);
            exists.Parameters.AddWithValue("name", table);
            if ((bool)(await exists.ExecuteScalarAsync(ct))!) await Run($"LOCK TABLE public.\"{table}\" IN SHARE MODE;");
        }
        var actual = await CatalogAsync(connection, pgTransaction, "public", ct);
        if (actual.Length == 0) throw new RequestFailure(409, "No existing baseline schema was found. Use dotnet ef database update for a new database.");

        // Build references from the actual migrations in a disposable schema in this transaction.
        // Rolling back to the savepoint removes it and restores search_path, preserving real data,
        // sequence positions, tenant activation and passwords. No extra database is required.
        await transaction.CreateSavepointAsync("reference_schema", ct);
        var reference = "ikd_verify_" + Guid.NewGuid().ToString("N");
        await Run($"CREATE SCHEMA \"{reference}\"; SET LOCAL search_path = \"{reference}\", pg_catalog;");
        var migrator = db.GetService<IMigrator>();
        await Run(migrator.GenerateScript("0", InitialMigration, MigrationsSqlGenerationOptions.NoTransactions));
        var initial = await CatalogAsync(connection, pgTransaction, reference, ct);
        await Run(migrator.GenerateScript(InitialMigration, AdministrationMigration, MigrationsSqlGenerationOptions.NoTransactions));
        var administration = await CatalogAsync(connection, pgTransaction, reference, ct);
        await Run(migrator.GenerateScript(AdministrationMigration, NtmMigration, MigrationsSqlGenerationOptions.NoTransactions));
        var ntm = await CatalogAsync(connection, pgTransaction, reference, ct);
        await Run(migrator.GenerateScript(NtmMigration, NtmWorkbookMigration, MigrationsSqlGenerationOptions.NoTransactions));
        var workbook = await CatalogAsync(connection, pgTransaction, reference, ct);
        await Run(migrator.GenerateScript(NtmWorkbookMigration, TenantStaffMigration, MigrationsSqlGenerationOptions.NoTransactions));
        var staff = await CatalogAsync(connection, pgTransaction, reference, ct);
        await transaction.RollbackToSavepointAsync("reference_schema", ct);
        await transaction.ReleaseSavepointAsync("reference_schema", ct);

        var matchesInitial = actual.SequenceEqual(initial, StringComparer.Ordinal);
        var matchesAdministration = actual.SequenceEqual(administration, StringComparer.Ordinal);
        var matchesNtm = actual.SequenceEqual(ntm, StringComparer.Ordinal);
        var matchesWorkbook = actual.SequenceEqual(workbook, StringComparer.Ordinal);
        var matchesStaff = actual.SequenceEqual(staff, StringComparer.Ordinal);
        if (!matchesInitial && !matchesAdministration && !matchesNtm && !matchesWorkbook && !matchesStaff)
        {
            var expected = actual.Any(x => x.StartsWith("table|ntm_", StringComparison.Ordinal)) ? workbook :
                actual.Any(x => x.StartsWith("table|membership_categories|", StringComparison.Ordinal)) ? administration : initial;
            // Report object identifiers/definitions only; never read application rows or credentials.
            var differences = expected.Except(actual).Select(x => "Missing or different: " + ObjectName(x))
                .Concat(actual.Except(expected).Select(x => "Unexpected or different: " + ObjectName(x))).Take(12);
            throw new RequestFailure(409, "Existing schema does not exactly match a supported baseline. No history was recorded. " + string.Join("; ", differences));
        }
        if (matchesInitial && applied.Contains(AdministrationMigration, StringComparer.Ordinal))
            throw new RequestFailure(409, "Migration history claims the administration upgrade, but its schema is absent. Adoption refused.");
        if (!matchesNtm && !matchesWorkbook && !matchesStaff && applied.Contains(NtmMigration, StringComparer.Ordinal))
            throw new RequestFailure(409, "Migration history claims the NTM upgrade, but its schema is absent. Adoption refused.");
        if (!matchesWorkbook && !matchesStaff && applied.Contains(NtmWorkbookMigration, StringComparer.Ordinal))
            throw new RequestFailure(409, "Migration history claims workbook alignment, but its schema is absent. Adoption refused.");
        if (!matchesStaff && applied.Contains(TenantStaffMigration, StringComparer.Ordinal))
            throw new RequestFailure(409, "Migration history claims tenant staff roles, but its schema is absent. Adoption refused.");

        await Run("""
            CREATE TABLE IF NOT EXISTS public."__EFMigrationsHistory" (
                "MigrationId" character varying(150) NOT NULL,
                "ProductVersion" character varying(32) NOT NULL,
                CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
            );
            """);
        var verified = matchesStaff ? new[] { InitialMigration, AdministrationMigration, NtmMigration, NtmWorkbookMigration, TenantStaffMigration } :
            matchesWorkbook ? [InitialMigration, AdministrationMigration, NtmMigration, NtmWorkbookMigration] :
            matchesNtm ? [InitialMigration, AdministrationMigration, NtmMigration] :
            matchesAdministration ? [InitialMigration, AdministrationMigration] : [InitialMigration];
        foreach (var migration in verified)
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                VALUES (@migration, @version) ON CONFLICT ("MigrationId") DO NOTHING
                """, connection, pgTransaction);
            insert.Parameters.AddWithValue("migration", migration);
            insert.Parameters.AddWithValue("version", ProductInfo.GetVersion());
            await insert.ExecuteNonQueryAsync(ct);
        }
        await transaction.CommitAsync(ct);
        return matchesStaff ? "Existing tenant-staff schema verified; all five migrations are recorded. No domain data was changed." :
            matchesWorkbook ? "Existing workbook-aligned NTM schema verified; all four migrations are recorded. No domain data was changed." :
            matchesNtm ? "Existing NTM schema verified; all three migrations are recorded. No domain data was changed." : matchesAdministration
            ? "Existing administration schema verified; both migrations are recorded. No domain data was changed."
            : "Existing original baseline verified; initial migration recorded. Run dotnet ef database update to apply the administration upgrade.";
    }
    private static string ObjectName(string definition) => string.Join("/", definition.Split('|').Take(3));

    private static async Task<string[]> CatalogAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string schema, CancellationToken ct)
    {
        const string sql = """
            SELECT 'table|' || c.relname || '|' || c.relkind::text || '|' || c.relrowsecurity || '|' || c.relforcerowsecurity AS definition
            FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
            WHERE n.nspname=@schema AND c.relname=ANY(@tables) AND c.relkind IN ('r','p','v','m','f')
            UNION ALL
            SELECT 'column|' || c.relname || '|' || a.attname || '|' || format_type(a.atttypid,a.atttypmod) || '|' || a.attnotnull || '|' ||
                coalesce(pg_get_expr(d.adbin,d.adrelid),'') || '|' || a.attidentity::text || '|' || a.attgenerated::text
            FROM pg_attribute a JOIN pg_class c ON c.oid=a.attrelid JOIN pg_namespace n ON n.oid=c.relnamespace
            LEFT JOIN pg_attrdef d ON d.adrelid=a.attrelid AND d.adnum=a.attnum
            WHERE n.nspname=@schema AND c.relname=ANY(@tables) AND a.attnum>0 AND NOT a.attisdropped
            UNION ALL
            SELECT 'constraint|' || c.relname || '|' || con.conname || '|' || pg_get_constraintdef(con.oid)
            FROM pg_constraint con JOIN pg_class c ON c.oid=con.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace
            WHERE n.nspname=@schema AND c.relname=ANY(@tables)
            UNION ALL
            SELECT 'index|' || tablename || '|' || indexname || '|' || indexdef FROM pg_indexes
            WHERE schemaname=@schema AND tablename=ANY(@tables)
            UNION ALL
            SELECT 'trigger|' || c.relname || '|' || t.tgname || '|' || pg_get_triggerdef(t.oid)
            FROM pg_trigger t JOIN pg_class c ON c.oid=t.tgrelid JOIN pg_namespace n ON n.oid=c.relnamespace
            WHERE n.nspname=@schema AND c.relname=ANY(@tables) AND NOT t.tgisinternal
            UNION ALL
            SELECT 'sequence|' || sequencename || '|' || data_type || '|' || start_value || '|' || min_value || '|' || max_value || '|' || increment_by || '|' || cycle || '|' || cache_size
            FROM pg_sequences WHERE schemaname=@schema AND sequencename='member_number_seq'
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("schema", schema); command.Parameters.AddWithValue("tables", Tables);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<string>();
        while (await reader.ReadAsync(ct)) rows.Add(reader.GetString(0).Replace("\"" + schema + "\".", "", StringComparison.Ordinal).Replace(schema + ".", "", StringComparison.Ordinal));
        return rows.Order(StringComparer.Ordinal).ToArray();
    }
}
