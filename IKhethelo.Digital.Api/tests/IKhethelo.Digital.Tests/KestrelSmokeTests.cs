using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using IKhethelo.Digital.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IKhethelo.Digital.Tests;

[Collection("PostgreSQL")]
public sealed class KestrelSmokeTests(ApiFixture fixture)
{
    [Fact]
    public async Task LiveDevelopmentKestrelServesSwaggerAndAcceptsBearerFromLogin()
    {
        var hostRoot = fixture.Factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var assembly = Path.Combine(hostRoot, "bin", configuration, "net10.0", "IKhethelo.Digital.Api.dll");
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = hostRoot, UseShellExecute = false,
            CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(assembly); start.ArgumentList.Add("--urls"); start.ArgumentList.Add($"http://127.0.0.1:{port}");
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["Email__Enabled"] = "false";
        start.Environment["ConnectionStrings__DefaultConnection"] = fixture.Connection;
        start.Environment["Jwt__Issuer"] = "ikd-tests"; start.Environment["Jwt__Audience"] = "ikd-admin-tests";
        start.Environment["Jwt__PrivateKeyPem"] = fixture.PrivateKey;
        start.Environment["Logging__LogLevel__Default"] = "Warning";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the API smoke-test process.");
        // Drain both streams without printing secrets or allowing the child's pipes to fill.
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(30) };
            var ready = false;
            for (var attempt = 0; attempt < 10 && !process.HasExited; attempt++)
            {
                try { ready = (await client.GetAsync("/swagger/index.html")).StatusCode == HttpStatusCode.OK; }
                catch (HttpRequestException) { }
                catch (TaskCanceledException) { }
                if (ready) break;
                await Task.Delay(500);
            }
            Assert.True(ready, "The isolated Development API did not become ready.");
            Assert.Contains("scheme: bearer", await client.GetStringAsync("/swagger/contract.yaml"));
            var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "tenant@example.com", password = fixture.Password });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/admin/tenants/eshlahleni/membership-applications")).StatusCode);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(); await output; await errors;
        }
    }
}
