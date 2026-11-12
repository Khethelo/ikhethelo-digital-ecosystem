using IKhethelo.Digital.Application;
using IKhethelo.Digital.Infrastructure.Persistence;

namespace IKhethelo.Digital.Api;

internal static class DatabaseSetupConsole
{
    public static async Task<int> AdoptAsync(IServiceProvider services)
    {
        try
        {
            using var scope = services.CreateScope();
            Console.WriteLine(await scope.ServiceProvider.GetRequiredService<ExistingDatabaseAdoption>().ExecuteAsync(CancellationToken.None));
            return 0;
        }
        catch (RequestFailure failure) { Console.Error.WriteLine(failure.Message); return 1; }
        catch (Exception)
        {
            Console.Error.WriteLine("Database adoption failed and was rolled back. Verify the protected connection, database availability and schema permissions. No credentials are printed."); return 1;
        }
    }
}
