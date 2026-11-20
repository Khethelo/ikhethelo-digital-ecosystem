using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using IKhethelo.Digital.Application;
using IKhethelo.Digital.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace IKhethelo.Digital.Tests;

[Collection("PostgreSQL")]
public sealed class IntegrationTests(ApiFixture fixture)
{
    private const string Public = "/api/v1/public/eshlahleni/membership-applications";
    private const string Admin = "/api/v1/admin/tenants/eshlahleni";
    private static ApplicationSubmission Submission() => new("Alice", "Example", new(1990, 1, 1),
        "+27" + Random.Shared.NextInt64(100000000, 999999999), Guid.NewGuid().ToString("N") + "@example.com", true, true);
    private async Task<HttpClient> AuthAsync(string email = "tenant@example.com")
    {
        var client = fixture.Factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = fixture.Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var token = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.Equal(1800, token.ExpiresIn); Assert.Equal("Bearer", token.TokenType);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.AccessToken);
        Assert.Equal(TimeSpan.FromMinutes(30), jwt.ValidTo - jwt.ValidFrom);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token.AccessToken); return client;
    }
    private async Task<Guid> SubmitAsync(ApplicationSubmission? submission = null, string? path = null)
    {
        var response = await fixture.Client.PostAsJsonAsync(path ?? Public, submission ?? Submission());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await JsonAsync(response)).GetProperty("applicationId").GetGuid();
    }
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    [Fact]
    public async Task DeclineIsTenantScopedAndDoesNotCreateMember()
    {
        var id = await SubmitAsync(); using var client = await AuthAsync();
        var path = $"{Admin}/membership-applications/{id}";
        var response = await client.PostAsJsonAsync(path + "/decline", new { reviewNotes = "Reviewed" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/activate", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/decline", new { })).StatusCode);
        await using var db = fixture.Db();
        var application = await db.Applications.SingleAsync(x => x.Id == id);
        Assert.Equal(MembershipApplicationStatus.LegacyClosed, application.Status);
        Assert.Equal(fixture.TenantAdminId, application.ReviewedByAdminId);
        Assert.NotNull(application.ReviewedAtUtc);
        Assert.False(await db.Members.AnyAsync(x => x.ApplicationId == id));
        Assert.True(await db.AuditLogs.AnyAsync(x => x.EntityId == id && x.Action == "MembershipApplicationRejected"));
    }

    [Fact]
    public async Task CanonicalTenantCodeSupportsTheRequestedAdministrativeWorkflow()
    {
        var id = await SubmitAsync(); using var client = await AuthAsync();
        var path = $"/api/v1/admin/tenants/ESHL/membership-applications/{id}";
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path + "/activate", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/admin/tenants/ESHL/members")).StatusCode);
    }

    [Fact]
    public async Task InternalRoleWithoutExplicitTenantGrantIsForbidden()
    {
        await using var factory = new ApiFactory(fixture.Connection, fixture.PrivateKey);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "internal@example.com", password = fixture.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken);
        var id = await SubmitAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Admin + "/members")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"{Admin}/membership-applications/{id}/activate", new { })).StatusCode);
        await using var db = fixture.Db();
        Assert.Equal(MembershipApplicationStatus.AwaitingInPersonCompletion, (await db.Applications.SingleAsync(x => x.Id == id)).Status);
    }

    [Fact]
    public async Task PublicSubmissionPersistsSafeServerAssignedData()
    {
        var submission = Submission() with { FirstName = "  Alice  ", Email = " Mixed" + Guid.NewGuid().ToString("N") + "@example.com " };
        var response = await fixture.Client.PostAsJsonAsync(Public, submission);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var receipt = await JsonAsync(response);
        Assert.Equal(new[] { "applicationId", "message", "status", "submittedAtUtc" }, receipt.EnumerateObject().Select(x => x.Name).Order().ToArray());
        Assert.Equal("AwaitingInPersonCompletion", receipt.GetProperty("status").GetString());
        await using var db = fixture.Db();
        var row = await db.Applications.SingleAsync(x => x.Id == receipt.GetProperty("applicationId").GetGuid());
        Assert.Equal(fixture.Eshl, row.TenantId); Assert.Equal("Alice", row.FirstName);
        Assert.Equal(Validation.NormalizeEmail(submission.Email!), row.NormalizedEmail);
        Assert.Null(row.ReviewedAtUtc); Assert.Null(row.ReviewedByAdminId);
        Assert.False(await db.Members.AnyAsync(x => x.ApplicationId == row.Id));
    }
    [Fact]
    public async Task EmailAndPhonePendingDuplicatesAreIndependentAndTenantScoped()
    {
        var submission = Submission(); await SubmitAsync(submission);
        var duplicateEmail = await fixture.Client.PostAsJsonAsync(Public, Submission() with { Email = submission.Email!.ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.Conflict, duplicateEmail.StatusCode);
        var duplicatePhone = await fixture.Client.PostAsJsonAsync(Public, Submission() with { PhoneNumber = submission.PhoneNumber });
        Assert.Equal(HttpStatusCode.Conflict, duplicatePhone.StatusCode);
        await SubmitAsync(submission, "/api/v1/public/second/membership-applications");
    }
    [Fact]
    public async Task DuplicateSubmissionRaceCreatesOneApplication()
    {
        var submission = Submission();
        var responses = await Task.WhenAll(fixture.Client.PostAsJsonAsync(Public, submission), fixture.Client.PostAsJsonAsync(Public, submission));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
    }
    [Fact]
    public async Task ValidationRejectsUnderageMissingConsentBadFormatsAndInjectedOwnership()
    {
        var underage = Submission() with { DateOfBirth = Validation.LocalDate(TimeProvider.System).AddYears(-17) };
        var response = await fixture.Client.PostAsJsonAsync(Public, underage);
        Assert.Equal((HttpStatusCode)422, response.StatusCode);
        Assert.True((await JsonAsync(response)).GetProperty("errors").TryGetProperty("dateOfBirth", out _));
        foreach (var body in new[] {
            "{}", "{\"firstName\":\"A\",\"email\":\"bad\"}" })
            Assert.Equal((HttpStatusCode)422, (await fixture.Client.PostAsync(Public, new StringContent(body, Encoding.UTF8, "application/json"))).StatusCode);
        foreach (var body in new[] { "{\"tenantId\":\"" + fixture.Second + "\"}", "{\"unexpected\":true}",
            "{\"firstName\":\"First\",\"firstName\":\"Last\"}" })
            Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsync(Public, new StringContent(body, Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal((HttpStatusCode)422, (await fixture.Client.PostAsJsonAsync(Public, Submission() with { PrivacyConsent = false })).StatusCode);
        Assert.Equal((HttpStatusCode)422, (await fixture.Client.PostAsJsonAsync(Public, Submission() with { DateOfBirth = new(9999, 1, 1) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsync(Public, new StringContent("{", Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsync(Public, new StringContent("{\"dateOfBirth\":\"not-a-date\"}", Encoding.UTF8, "application/json"))).StatusCode);
    }
    [Theory]
    [InlineData("unknown")]
    [InlineData("inactive")]
    public async Task UnknownAndInactiveTenantsReturn404(string tenant) =>
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Client.PostAsJsonAsync($"/api/v1/public/{tenant}/membership-applications", Submission())).StatusCode);

    [Fact]
    public async Task ApprovalIsAtomicAndDoubleApprovalCreatesOneMember()
    {
        var id = await SubmitAsync(); using var client = await AuthAsync();
        using var otherAdmin = await AuthAsync("internal@example.com");
        var path = $"{Admin}/membership-applications/{id}/activate";
        var responses = await Task.WhenAll(client.PostAsJsonAsync(path, new { reviewNotes = "Checked" }), otherAdmin.PostAsJsonAsync(path, new { }));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        var member = await JsonAsync(responses.Single(x => x.StatusCode == HttpStatusCode.OK));
        Assert.Matches("^ESC-[0-9]{5,}$", member.GetProperty("memberNumber").GetString()!);
        Assert.Equal("Active", member.GetProperty("status").GetString()); Assert.False(member.TryGetProperty("tenantId", out _));
        await using var db = fixture.Db(); var application = await db.Applications.SingleAsync(x => x.Id == id);
        Assert.Equal(MembershipApplicationStatus.Completed, application.Status); Assert.NotNull(application.ReviewedAtUtc);
        Assert.Contains(application.ReviewedByAdminId!.Value, new[] { fixture.TenantAdminId, fixture.InternalAdminId });
        Assert.Equal(1, await db.Members.CountAsync(x => x.ApplicationId == id));
        Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.EntityId == id && x.Action == "MembershipActivated"));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"{Admin}/membership-applications/{id}/activate", new { })).StatusCode);
    }
    [Fact]
    public async Task SeparateConcurrentApprovalsAllocateDistinctSequenceNumbers()
    {
        var first = await SubmitAsync(); var second = await SubmitAsync(); using var client = await AuthAsync();
        var responses = await Task.WhenAll(client.PostAsJsonAsync($"{Admin}/membership-applications/{first}/activate", new { }),
            client.PostAsJsonAsync($"{Admin}/membership-applications/{second}/activate", new { }));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var numbers = await Task.WhenAll(responses.Select(async response => (await JsonAsync(response)).GetProperty("memberNumber").GetString()));
        Assert.NotEqual(numbers[0], numbers[1]);
    }
    [Fact]
    public async Task ConcurrentActivationHasOneTerminalState()
    {
        var id = await SubmitAsync(); using var client = await AuthAsync();
        var responses = await Task.WhenAll(client.PostAsJsonAsync($"{Admin}/membership-applications/{id}/activate", new { }),
            client.PostAsJsonAsync($"{Admin}/membership-applications/{id}/activate", new { }));
        Assert.Single(responses, x => x.StatusCode == HttpStatusCode.OK); Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
        await using var db = fixture.Db(); var application = await db.Applications.SingleAsync(x => x.Id == id);
        Assert.Equal(application.Status == MembershipApplicationStatus.Completed ? 1 : 0, await db.Members.CountAsync(x => x.ApplicationId == id));
    }
    [Fact]
    public async Task TenantAdminCannotReadReviewEditOrDeleteOtherTenantResources()
    {
        var otherId = await SubmitAsync(path: "/api/v1/public/second/membership-applications");
        using var client = await AuthAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Admin}/membership-applications/{otherId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"{Admin}/membership-applications/{otherId}/activate", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/tenants/second/membership-applications")).StatusCode);
        using var second = await AuthAsync("second@example.com");
        var approved = await second.PostAsJsonAsync($"/api/v1/admin/tenants/second/membership-applications/{otherId}/activate", new { });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode); var memberId = (await JsonAsync(approved)).GetProperty("memberId").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Admin}/members/{memberId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"{Admin}/members/{memberId}", new { firstName = "Edited", lastName = "Other", email = "other@example.com", phoneNumber = "+27829999999" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"{Admin}/members/{memberId}/status", new { status = "Inactive" })).StatusCode);
        Assert.Contains((await client.DeleteAsync($"{Admin}/members/{memberId}")).StatusCode, new[] { HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound });
        var visible = await JsonAsync(await client.GetAsync($"{Admin}/membership-applications?search={otherId}")); Assert.Equal(0, visible.GetProperty("totalCount").GetInt64());
        await using var db = fixture.Db(); Assert.Equal(MemberStatus.Active, (await db.Members.SingleAsync(x => x.Id == memberId)).Status);
    }
    [Fact]
    public async Task InternalAdminExplicitTenantReadsAndWritesAreAudited()
    {
        var id = await SubmitAsync(path: "/api/v1/public/second/membership-applications"); using var client = await AuthAsync("internal@example.com");
        var path = $"/api/v1/admin/tenants/second/membership-applications/{id}";
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path + "/activate", new { })).StatusCode);
        await using var db = fixture.Db(); var audits = await db.AuditLogs.Where(x => x.EntityId == id && x.ActorAdminUserId == fixture.InternalAdminId).ToListAsync();
        Assert.Contains(audits, x => x.Action == "CrossTenantRead"); Assert.Contains(audits, x => x.Action == "MembershipActivated");
        Assert.All(audits, x => { Assert.Equal(fixture.Second, x.TenantId); Assert.Contains("crossTenant", x.MetadataJson); });
    }
    [Fact]
    public async Task MemberUpdatesAreWhitelistedAndActiveConflictsBlockReactivation()
    {
        var submission = Submission(); var first = await SubmitAsync(submission); using var client = await AuthAsync();
        var approved = await JsonAsync(await client.PostAsJsonAsync($"{Admin}/membership-applications/{first}/activate", new { }));
        var member = approved.GetProperty("memberId").GetGuid(); var path = $"{Admin}/members/{member}";
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(path + "/status", new { status = "Suspended" })).StatusCode);
        var next = await SubmitAsync(submission);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"{Admin}/membership-applications/{next}/activate", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(path + "/status", new { status = "Active" })).StatusCode);
        Assert.Equal((HttpStatusCode)422, (await client.PutAsJsonAsync(path + "/status", new { status = "active" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, new { tenantId = fixture.Second })).StatusCode);
        var updated = await client.PutAsJsonAsync(path, new { firstName = "Changed", lastName = "Contact", phoneNumber = "+27820000001", email = "changed" + Guid.NewGuid().ToString("N") + "@example.com" });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        await using var db = fixture.Db(); Assert.Equal(submission.FirstName, (await db.Applications.SingleAsync(x => x.Id == first)).FirstName);
        Assert.True(await db.AuditLogs.AnyAsync(x => x.EntityId == member && x.Action == "UpdateMemberStatus"));
    }
    [Fact]
    public async Task ActiveMemberConflictDoesNotApproveOrAllocateAnotherMember()
    {
        var submission = Submission(); var first = await SubmitAsync(submission); using var client = await AuthAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"{Admin}/membership-applications/{first}/activate", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.PostAsJsonAsync(Public, submission)).StatusCode);
        await using var db = fixture.Db(); Assert.Equal(1, await db.Members.CountAsync(x => x.ApplicationId == first));
    }
    [Fact]
    public async Task LoginAndBearerFailuresAreSafeAndInactiveAccountsAreRejected()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync(Admin + "/members")).StatusCode);
        var bad = await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new { email = "tenant@example.com", password = "wrong" });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        Assert.True(bad.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new { email = "missing@example.com", password = "wrong" })).StatusCode);
        using var client = await AuthAsync();
        await using (var db = fixture.Db()) { var admin = await db.AdminUsers.SingleAsync(x => x.Id == fixture.TenantAdminId); admin.IsActive = false; await db.SaveChangesAsync(); }
        try
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Admin + "/members")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new { email = "tenant@example.com", password = fixture.Password })).StatusCode);
        }
        finally { await using var db = fixture.Db(); var admin = await db.AdminUsers.SingleAsync(x => x.Id == fixture.TenantAdminId); admin.IsActive = true; await db.SaveChangesAsync(); }
        var problem = await bad.Content.ReadAsStringAsync(); Assert.DoesNotContain("password_hash", problem); Assert.DoesNotContain("Npgsql", problem);
    }
    [Fact]
    public async Task ForgedExpiredWrongAudienceAndContradictoryTenantTokensFail()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(); rsa.ImportFromPem(fixture.PrivateKey);
        var credentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
        foreach (var kind in new[] { "expired", "audience", "issuer", "tenant", "forged" })
        {
            var now = DateTime.UtcNow; var claims = new[] { new Claim("sub", fixture.TenantAdminId.ToString()), new Claim("role", "TenantAdmin"), new Claim("tenant_id", kind == "tenant" ? fixture.Second.ToString() : fixture.Eshl.ToString()) };
            using var wrongKey = System.Security.Cryptography.RSA.Create(2048);
            var token = new JwtSecurityToken(kind == "issuer" ? "wrong" : "ikd-tests", kind == "audience" ? "wrong" : "ikd-admin-tests", claims,
                now.AddHours(-1), kind == "expired" ? now.AddMinutes(-1) : now.AddMinutes(5), kind == "forged" ? new(new RsaSecurityKey(wrongKey), SecurityAlgorithms.RsaSha256) : credentials);
            using var client = fixture.Factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
            client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Admin + "/members")).StatusCode);
        }
    }
    [Fact]
    public async Task TransportLimitsCorsAndRateLimitsHaveSafeResponses()
    {
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await fixture.Client.PostAsync(Public, new StringContent(new string(' ', 17000), Encoding.UTF8, "application/json"))).StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await fixture.Client.PostAsync(Public, new StringContent("{}"))).StatusCode);
        using var preflight = new HttpRequestMessage(HttpMethod.Options, Public); preflight.Headers.Add("Origin", "http://localhost:4200"); preflight.Headers.Add("Access-Control-Request-Method", "POST");
        Assert.Equal("http://localhost:4200", (await fixture.Client.SendAsync(preflight)).Headers.GetValues("Access-Control-Allow-Origin").Single());
        using var denied = new HttpRequestMessage(HttpMethod.Options, Public); denied.Headers.Add("Origin", "https://unapproved.example"); denied.Headers.Add("Access-Control-Request-Method", "POST");
        Assert.False((await fixture.Client.SendAsync(denied)).Headers.Contains("Access-Control-Allow-Origin"));
        await using var limitedFactory = new ApiFactory(fixture.Connection, fixture.PrivateKey, limit: 1); using var limited = limitedFactory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.Created, (await limited.PostAsJsonAsync(Public, Submission())).StatusCode);
        var response = await limited.PostAsJsonAsync(Public, Submission()); Assert.Equal((HttpStatusCode)429, response.StatusCode); Assert.NotNull(response.Headers.RetryAfter);
        using var client = await AuthAsync(); Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Admin + "/members?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Admin + "/members?status=0")).StatusCode);
    }
}
