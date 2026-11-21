using System.Net;
using Npgsql;

namespace IKhethelo.Digital.Tests;

public static class LocalTestPostgres
{
    public static string RequireLoopback(string connection)
    {
        var host = new NpgsqlConnectionStringBuilder(connection).Host;
        if (!IPAddress.TryParse(host, out var address) || !IPAddress.IsLoopback(address))
            throw new InvalidOperationException("IKD_TEST_POSTGRES must use a numeric loopback host (127.0.0.1 or ::1).");
        return connection;
    }
}
