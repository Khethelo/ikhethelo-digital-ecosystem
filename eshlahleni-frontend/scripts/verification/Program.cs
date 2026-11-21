using IKhethelo.Digital.Domain;
using IKhethelo.Digital.Infrastructure.Identity;
using IKhethelo.Digital.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using IKhethelo.Digital.Tests;

var control = LocalTestPostgres.RequireLoopback(Environment.GetEnvironmentVariable("IKD_TEST_POSTGRES") ?? throw new InvalidOperationException("Provide an isolated PostgreSQL test connection."));
var database = Environment.GetEnvironmentVariable("ESHL_TEST_DATABASE") ?? "";
if (!System.Text.RegularExpressions.Regex.IsMatch(database, "^eshl_frontend_e2e_[a-f0-9]{32}$"))
    throw new InvalidOperationException("Only freshly named frontend verification databases are supported.");
try
{
    await using var connection = new NpgsqlConnection(control);
    await connection.OpenAsync();
    if (args.Contains("cleanup"))
    {
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {database} WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync(); Console.WriteLine("Disposable frontend verification database removed."); return;
    }
    await using (var create = new NpgsqlCommand($"CREATE DATABASE {database}", connection)) await create.ExecuteNonQueryAsync();
    var settings = new NpgsqlConnectionStringBuilder(control) { Database = database, Pooling = false };
    await using var db = new DigitalDbContext(new DbContextOptionsBuilder<DigitalDbContext>().UseNpgsql(settings.ConnectionString).Options);
    await db.Database.MigrateAsync();
    var email = Environment.GetEnvironmentVariable("ESHL_E2E_EMAIL") ?? throw new InvalidOperationException("Test account email missing.");
    var password = Environment.GetEnvironmentVariable("ESHL_E2E_PASSWORD") ?? throw new InvalidOperationException("Test account password missing.");
    await new BootstrapAdministrator(db, new PasswordHasher<AdminUser>(), TimeProvider.System)
        .ExecuteAsync("ESHL", email, password, CancellationToken.None);
    Console.WriteLine("Isolated frontend verification schema and temporary administrator prepared.");
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Frontend verification setup failed ({exception.GetType().Name}). No credentials are printed.");
    Environment.ExitCode = 1;
}
