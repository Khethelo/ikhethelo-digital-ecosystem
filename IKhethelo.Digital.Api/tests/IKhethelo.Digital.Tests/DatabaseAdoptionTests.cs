using System.Net;
using System.Net.Http.Json;
using IKhethelo.Digital.Application;
using IKhethelo.Digital.Domain;
using IKhethelo.Digital.Infrastructure.Identity;
using IKhethelo.Digital.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace IKhethelo.Digital.Tests;

[Collection("PostgreSQL")]
public sealed class DatabaseAdoptionTests(ApiFixture fixture)
{
    [Fact]
    public async Task OriginalBaselineAdoptionPreservesDataAndSequenceAndAllowsAdditiveUpgrade()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Db();
        await db.GetService<IMigrator>().MigrateAsync(ExistingDatabaseAdoption.InitialMigration);
        await db.Database.ExecuteSqlRawAsync("DROP TABLE \"__EFMigrationsHistory\"");
        var admin = Guid.NewGuid(); var application = Guid.NewGuid(); var member = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO admin_users (id,tenant_id,email,normalized_email,password_hash,role)
                VALUES ({admin},{fixture.Eshl},'existing@example.com','EXISTING@EXAMPLE.COM','test-hash-to-preserve','TenantAdmin');
            INSERT INTO membership_applications (id,tenant_id,first_name,last_name,date_of_birth,phone_number,normalized_phone_number,
                email,normalized_email,status,terms_accepted,privacy_consent,reviewed_at_utc,reviewed_by_admin_id)
                VALUES ({application},{fixture.Eshl},'Existing','Member',DATE '1990-01-01','+27821234567','+27821234567',
                'member@example.com','MEMBER@EXAMPLE.COM','Approved',true,true,CURRENT_TIMESTAMP,{admin});
            INSERT INTO members (id,tenant_id,application_id,member_number,first_name,last_name,date_of_birth,phone_number,normalized_phone_number,email,normalized_email)
                VALUES ({member},{fixture.Eshl},{application},'ESC-00420','Existing','Member',DATE '1990-01-01','+27821234567','+27821234567','member@example.com','MEMBER@EXAMPLE.COM');
            UPDATE tenants SET is_active=false WHERE id={fixture.Eshl};
            SELECT setval('member_number_seq',420,true);
            """);
        var result = await new ExistingDatabaseAdoption(db).ExecuteAsync(CancellationToken.None);
        Assert.Contains("initial migration recorded", result);
        Assert.Equal(new[] { ExistingDatabaseAdoption.InitialMigration }, await db.Database.GetAppliedMigrationsAsync());
        // Repeating adoption verifies the schema again and does not duplicate history.
        await new ExistingDatabaseAdoption(db).ExecuteAsync(CancellationToken.None);
        await db.Database.MigrateAsync();
        Assert.Equal(4, (await db.Database.GetAppliedMigrationsAsync()).Count());
        var saved = await db.AdminUsers.SingleAsync(x => x.Id == admin); Assert.Equal("test-hash-to-preserve", saved.PasswordHash); Assert.Equal(0, saved.SecurityVersion);
        var savedMember = await db.Members.SingleAsync(x => x.Id == member); Assert.Equal("ESC-00420", savedMember.MemberNumber); Assert.Null(savedMember.CategoryId);
        Assert.False((await db.Tenants.SingleAsync(x => x.Id == fixture.Eshl)).IsActive);
        Assert.Equal(421, await db.Database.SqlQueryRaw<long>("SELECT nextval('member_number_seq') AS \"Value\"").SingleAsync());
        await AssertNoReferenceSchema(db);
    }
    [Fact]
    public async Task CurrentSqlBaselineAdoptionAllowsBootstrapAndExistingLoginApi()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.Db();
        await db.Database.ExecuteSqlRawAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "baseline.sql")));
        var result = await new ExistingDatabaseAdoption(db).ExecuteAsync(CancellationToken.None);
        Assert.Contains("both migrations", result);
        Assert.Equal(new[] { ExistingDatabaseAdoption.NtmMigration, ExistingDatabaseAdoption.NtmWorkbookMigration }, await db.Database.GetPendingMigrationsAsync());
        await db.Database.MigrateAsync();
        var password = "Random-" + Guid.NewGuid().ToString("N");
        var id = await new BootstrapAdministrator(db, new PasswordHasher<AdminUser>(), TimeProvider.System)
            .ExecuteAsync("ESHL", "bootstrap@example.com", password, CancellationToken.None);
        Assert.True(await db.AuditLogs.AnyAsync(x => x.EntityId == id && x.Action == "BootstrapAdministrator"));
        await using var factory = new ApiFactory(database.Connection, fixture.PrivateKey);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "bootstrap@example.com", password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.NotEmpty((await login.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken);
        await AssertNoReferenceSchema(db);
    }
    [Theory]
    [InlineData("DROP INDEX ux_membership_applications_pending_email")]
    [InlineData("ALTER TABLE admin_users ADD security_version integer NOT NULL DEFAULT 0")]
    public async Task DriftOrPartialUpgradeIsRefusedWithoutRecordingHistory(string change)
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Db();
        await db.GetService<IMigrator>().MigrateAsync(ExistingDatabaseAdoption.InitialMigration);
        await db.Database.ExecuteSqlRawAsync("DROP TABLE \"__EFMigrationsHistory\"");
        await db.Database.ExecuteSqlRawAsync(change);
        var failure = await Assert.ThrowsAsync<RequestFailure>(() => new ExistingDatabaseAdoption(db).ExecuteAsync(CancellationToken.None));
        Assert.Equal(409, failure.Status); Assert.Contains("does not exactly match", failure.Message);
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM pg_tables WHERE schemaname='public' AND tablename='__EFMigrationsHistory'").SingleAsync());
        Assert.True(await db.Tenants.AnyAsync(x => x.Id == fixture.Eshl));
        await AssertNoReferenceSchema(db);
    }
    [Fact]
    public async Task NtmSchemaAdoptionVerifiesAllFourMigrationsAndPreservesTenantIdentity()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Db();
        await db.Database.MigrateAsync();
        var tenantId = await db.Tenants.Where(x => x.Code == "NTM").Select(x => x.Id).SingleAsync();
        Assert.NotEqual(Guid.Empty, tenantId);
        await db.Database.ExecuteSqlRawAsync("DROP TABLE \"__EFMigrationsHistory\"");
        var result = await new ExistingDatabaseAdoption(db).ExecuteAsync(CancellationToken.None);
        Assert.Contains("all four", result); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(tenantId, await db.Tenants.Where(x => x.Code == "NTM").Select(x => x.Id).SingleAsync());
        await AssertNoReferenceSchema(db);
    }
    [Fact]
    public async Task WorkbookAlignmentRefusesNumericLossAndPreservesRecordsAfterExplicitCorrection()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Db();
        await db.GetService<IMigrator>().MigrateAsync(ExistingDatabaseAdoption.NtmMigration);
        var veteran = Guid.NewGuid(); var business = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO ntm_veterans(id,tenant_id,full_name,mkmva_number)
            VALUES ({veteran},{fixture.Eshl},'Alignment test','ALIGNMENT-TEST');
            INSERT INTO ntm_businesses(id,tenant_id,veteran_id,business_name,years_in_operation,funding_amount)
            VALUES ({business},{fixture.Eshl},{veteran},'Alignment test',1.25,12.345);
            """);
        var failure = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync());
        Assert.Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        Assert.DoesNotContain(ExistingDatabaseAdoption.NtmWorkbookMigration, await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(12.345m, await db.Database.SqlQuery<decimal>($"SELECT funding_amount AS \"Value\" FROM ntm_businesses WHERE id={business}").SingleAsync());
        // Explicit reconciliation, not rounding in the migration.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ntm_businesses SET years_in_operation=1.2, funding_amount=12.34 WHERE id={business}");
        await db.Database.MigrateAsync();
        var row = await db.NtmBusinesses.SingleAsync(x => x.Id == business);
        Assert.Equal(12.34m, row.FundingAmount); Assert.Equal(veteran, row.VeteranId); Assert.Null(row.NumberOfEmployeesSource);
    }
    [Fact]
    public async Task EmptyDatabaseIsNotStampedAsAnExistingBaseline()
    {
        await using var database = await TestDatabase.CreateAsync(); await using var db = database.Db();
        var failure = await Assert.ThrowsAsync<RequestFailure>(() => new ExistingDatabaseAdoption(db).ExecuteAsync(CancellationToken.None));
        Assert.Equal(409, failure.Status); Assert.Contains("new database", failure.Message);
        Assert.Empty(await db.Database.GetAppliedMigrationsAsync());
    }
    private static async Task AssertNoReferenceSchema(DigitalDbContext db) => Assert.Equal(0,
        await db.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM pg_namespace WHERE nspname LIKE 'ikd_verify_%'").SingleAsync());

    private sealed class TestDatabase(string connection, string control, string name) : IAsyncDisposable
    {
        public string Connection { get; } = connection;
        public DigitalDbContext Db() => new(new DbContextOptionsBuilder<DigitalDbContext>().UseNpgsql(Connection).Options);
        public static async Task<TestDatabase> CreateAsync()
        {
            var control = Environment.GetEnvironmentVariable("IKD_TEST_POSTGRES") ?? throw new InvalidOperationException("Configure isolated test PostgreSQL.");
            var name = "ikd_adoption_" + Guid.NewGuid().ToString("N");
            await ApiFixture.ExecuteAsync(control, $"CREATE DATABASE {name}");
            return new(new NpgsqlConnectionStringBuilder(control) { Database = name, Pooling = false }.ConnectionString, control, name);
        }
        public async ValueTask DisposeAsync() => await ApiFixture.ExecuteAsync(control, $"DROP DATABASE {name} WITH (FORCE)");
    }
}
