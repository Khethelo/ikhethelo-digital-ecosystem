using System.Security.Cryptography;
using IKhethelo.Digital.Domain;
using IKhethelo.Digital.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace IKhethelo.Digital.Tests;

[CollectionDefinition("PostgreSQL", DisableParallelization = true)]
public sealed class PostgreSqlCollection : ICollectionFixture<ApiFixture>;

public sealed class ApiFactory(string connection, string key, string environment = "Development", int limit = 10000, Guid? internalAdmin = null, bool manageAdmins = false) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connection,
            ["Jwt:Issuer"] = "ikd-tests", ["Jwt:Audience"] = "ikd-admin-tests", ["Jwt:PrivateKeyPem"] = key,
            ["Tenancy:Aliases:second"] = "SECOND", ["Tenancy:Aliases:inactive"] = "INACTIVE",
            [$"Tenancy:CrossTenantMembership:{internalAdmin ?? Guid.Empty}:0"] = "ESHL",
            [$"Tenancy:CrossTenantMembership:{internalAdmin ?? Guid.Empty}:1"] = "SECOND",
            [$"Tenancy:AdminUserManagement:{(manageAdmins ? internalAdmin : null) ?? Guid.Empty}:0"] = "ESHL",
            [$"Tenancy:AdminUserManagement:{(manageAdmins ? internalAdmin : null) ?? Guid.Empty}:1"] = "SECOND",
            ["RateLimits:PerMinute"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture)
        }));
    }
}

public sealed class ApiFixture : IAsyncLifetime
{
    private readonly string databaseName = "ikd_test_" + Guid.NewGuid().ToString("N");
    private readonly string baselineName = "ikd_baseline_" + Guid.NewGuid().ToString("N");
    private string adminConnection = "";
    public string Connection { get; private set; } = "";
    public string BaselineConnection { get; private set; } = "";
    public string PrivateKey { get; } = RSA.Create(2048).ExportRSAPrivateKeyPem();
    public string Password { get; } = "Test-" + Guid.NewGuid().ToString("N");
    public Guid Eshl { get; } = Guid.Parse("e5a11111-5c1a-4e5a-9e5a-111111111111");
    public Guid Second { get; } = Guid.NewGuid();
    public Guid TenantAdminId { get; } = Guid.NewGuid();
    public Guid InternalAdminId { get; } = Guid.NewGuid();
    public ApiFactory Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public DigitalDbContext Db() => new(new DbContextOptionsBuilder<DigitalDbContext>().UseNpgsql(Connection).Options);
    public async Task InitializeAsync()
    {
        adminConnection = Environment.GetEnvironmentVariable("IKD_TEST_POSTGRES") ??
            throw new InvalidOperationException("Set IKD_TEST_POSTGRES to an isolated PostgreSQL connection with CREATE DATABASE permission; tests never skip database verification.");
        var builder = new NpgsqlConnectionStringBuilder(adminConnection)
        {
            Database = databaseName, Pooling = true, MaxPoolSize = 20, Timeout = 60, CommandTimeout = 120
        };
        Connection = builder.ConnectionString; builder.Database = baselineName; BaselineConnection = builder.ConnectionString;
        await ExecuteAsync(adminConnection, $"CREATE DATABASE {databaseName}");
        await ExecuteAsync(adminConnection, $"CREATE DATABASE {baselineName}");
        await using (var db = Db()) await db.Database.MigrateAsync();
        await ExecuteAsync(BaselineConnection, await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "baseline.sql")));
        await using (var db = Db())
        {
            var now = DateTimeOffset.UtcNow;
            db.Tenants.AddRange(new Tenant { Id = Second, Code = "SECOND", Name = "Second test tenant", IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now },
                new Tenant { Id = Guid.NewGuid(), Code = "INACTIVE", Name = "Inactive test tenant", IsActive = false, CreatedAtUtc = now, UpdatedAtUtc = now });
            var hasher = new PasswordHasher<AdminUser>();
            foreach (var admin in new[] {
                new AdminUser { Id = TenantAdminId, TenantId = Eshl, Email = "tenant@example.com", Role = AdminRole.TenantAdmin },
                new AdminUser { Id = Guid.NewGuid(), TenantId = Second, Email = "second@example.com", Role = AdminRole.TenantAdmin },
                new AdminUser { Id = InternalAdminId, Email = "internal@example.com", Role = AdminRole.IKhetheloAdmin } })
            {
                admin.NormalizedEmail = admin.Email.ToUpperInvariant(); admin.IsActive = true;
                admin.PasswordHash = hasher.HashPassword(admin, Password); admin.CreatedAtUtc = now; admin.UpdatedAtUtc = now; db.AdminUsers.Add(admin);
            }
            await db.SaveChangesAsync();
        }
        Factory = new(Connection, PrivateKey, internalAdmin: InternalAdminId); Client = Factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }
    public static async Task ExecuteAsync(string connection, string sql)
    {
        await using var conn = new NpgsqlConnection(connection); await conn.OpenAsync();
        await using var command = new NpgsqlCommand(sql, conn); await command.ExecuteNonQueryAsync();
    }
    public async Task DisposeAsync()
    {
        Client?.Dispose(); if (Factory is not null) await Factory.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        // Names are freshly generated by this fixture, never provided as arbitrary cleanup targets.
        if (adminConnection.Length != 0)
        {
            await ExecuteAsync(adminConnection, $"DROP DATABASE IF EXISTS {databaseName} WITH (FORCE)");
            await ExecuteAsync(adminConnection, $"DROP DATABASE IF EXISTS {baselineName} WITH (FORCE)");
        }
    }
}
