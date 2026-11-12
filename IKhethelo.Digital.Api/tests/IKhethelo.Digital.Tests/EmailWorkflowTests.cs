using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IKhethelo.Digital.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace IKhethelo.Digital.Tests;

[Collection("PostgreSQL")]
public sealed class EmailWorkflowTests(ApiFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NotificationsObserveCommittedStateAndFailuresPreserveSuccess(bool fail)
    {
        var email = Guid.NewGuid().ToString("N") + "@example.test";
        var sender = new CommittedStateSender(fixture, fail);
        await using var factory = fixture.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailSender>(); services.AddSingleton<IEmailSender>(sender);
        }));
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        const string path = "/api/v1/public/eshlahleni/membership-applications";
        var submission = new ApplicationSubmission("Email", "Workflow", new(1990, 1, 1),
            "+27" + Random.Shared.NextInt64(100000000, 999999999), email, true, true);
        var response = await client.PostAsJsonAsync(path, submission);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var receipt = await response.Content.ReadFromJsonAsync<JsonElement>();
        var applicationId = receipt.GetProperty("applicationId").GetGuid();
        Assert.Equal(1, sender.Calls); Assert.True(sender.SawRegistrationCommit);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, submission)).StatusCode);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "tenant@example.com", password = fixture.Password });
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken);
        var activation = $"/api/v1/admin/tenants/ESHL/membership-applications/{applicationId}/activate";
        var activated = await client.PostAsJsonAsync(activation, new { });
        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);
        var member = await activated.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, sender.Calls); Assert.True(sender.SawActivationCommit);
        Assert.Contains(member.GetProperty("memberNumber").GetString()!, sender.LastBody);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(activation, new { })).StatusCode);
        Assert.Equal(2, sender.Calls);
        await using var db = fixture.Db();
        Assert.Equal(1, await db.Members.CountAsync(x => x.ApplicationId == applicationId));
        Assert.True(await db.AuditLogs.AnyAsync(x => x.EntityId == applicationId && x.Action == "MembershipActivated"));
    }

    private sealed class CommittedStateSender(ApiFixture fixture, bool fail) : IEmailSender
    {
        public int Calls { get; private set; }
        public bool SawRegistrationCommit { get; private set; }
        public bool SawActivationCommit { get; private set; }
        public string LastBody { get; private set; } = "";
        public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
        {
            Calls++; LastBody = htmlBody;
            // Independent context/connection proves the notification runs after commit.
            await using var db = fixture.Db();
            var registration = await db.Applications.SingleAsync(x => x.Email == to, cancellationToken);
            if (Calls == 1) SawRegistrationCommit = !await db.Members.AnyAsync(x => x.ApplicationId == registration.Id, cancellationToken);
            else SawActivationCommit = await db.Members.AnyAsync(x => x.ApplicationId == registration.Id, cancellationToken);
            if (fail) throw new InvalidOperationException("Simulated SMTP outage");
        }
    }
}
