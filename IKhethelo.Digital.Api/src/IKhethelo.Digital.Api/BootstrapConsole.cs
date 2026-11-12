using System.Text;
using IKhethelo.Digital.Application;
using IKhethelo.Digital.Infrastructure.Identity;
using IKhethelo.Digital.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IKhethelo.Digital.Api;

internal static class BootstrapConsole
{
    public static async Task<int> RunAsync(IServiceProvider services, string tenantCode)
    {
        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Bootstrap requires an interactive terminal. Passwords cannot be supplied in arguments or redirected input.");
            return 1;
        }
        try
        {
            using var scope = services.CreateScope();
            if ((await scope.ServiceProvider.GetRequiredService<DigitalDbContext>().Database.GetPendingMigrationsAsync()).Any())
            {
                Console.Error.WriteLine("Database setup is incomplete. If the database was installed from baseline SQL, first run --adopt-existing-database; then run dotnet ef database update before bootstrap.");
                return 1;
            }
        }
        catch (Exception)
        {
            Console.Error.WriteLine("Cannot check database setup. Verify the protected connection string and PostgreSQL availability before bootstrap."); return 1;
        }
        Console.Write($"Initial TenantAdmin for {tenantCode}. Email: "); var email = Console.ReadLine();
        Console.Write("Password (12–128 characters): "); var password = ReadPassword();
        Console.Write("Confirm password: "); var confirmation = ReadPassword();
        if (password != confirmation) { Console.Error.WriteLine("Passwords do not match."); return 1; }
        try
        {
            using var scope = services.CreateScope();
            var id = await scope.ServiceProvider.GetRequiredService<BootstrapAdministrator>().ExecuteAsync(tenantCode, email ?? "", password, CancellationToken.None);
            Console.WriteLine($"Administrator {id} created and audited. Use POST /api/v1/auth/login to sign in."); return 0;
        }
        catch (RequestFailure failure)
        {
            Console.Error.WriteLine(failure.Status == 409 ? "Bootstrap refused: the tenant already has an administrator or the email is already in use." : failure.Message); return 1;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("Bootstrap failed. Check the protected database configuration, applied migrations and database availability. No credentials are printed."); return 1;
        }
    }
    private static string ReadPassword()
    {
        var buffer = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return buffer.ToString(); }
            if (key.Key == ConsoleKey.Backspace) { if (buffer.Length > 0) buffer.Length--; }
            else if (!char.IsControl(key.KeyChar) && buffer.Length < 256) buffer.Append(key.KeyChar);
        }
    }
}
