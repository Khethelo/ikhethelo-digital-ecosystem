using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IKhethelo.Digital.Tests;

public sealed class HealthCheckTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HealthCheckIsAnonymousAndDoesNotRequireDatabase(bool missingConnection)
    {
        using var rsa = RSA.Create(2048);
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = missingConnection ? null : "Host=127.0.0.1;Port=1;Database=unavailable;Username=unused;Password=unused;Timeout=1",
            ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience",
            ["Jwt:PrivateKeyPem"] = rsa.ExportRSAPrivateKeyPem(),
            ["Email:Enabled"] = "false",
            ["ReverseProxy:EdgeTerminatesTls"] = "true",
            ["AllowedHosts"] = "*"
        };
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings));
            builder.ConfigureServices(services => services.AddDataProtection().UseEphemeralDataProtectionProvider());
        });
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/healthz"));
        Assert.Equal(HttpStatusCode.OK, head.StatusCode);
    }
}
