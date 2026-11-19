using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using IKhethelo.Digital.Application;
using IKhethelo.Digital.Domain;
using IKhethelo.Digital.Infrastructure.Identity;
using IKhethelo.Digital.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IKhethelo.Digital.Tests;

[Collection("PostgreSQL")]
public sealed class AdministrationTests(ApiFixture fixture)
{
    private const string Tenant = "/api/v1/admin/tenants/ESHL";
    private static string Email() => Guid.NewGuid().ToString("N") + "@example.com";
    private static string Password() => "Random-" + Guid.NewGuid().ToString("N");
    private static MemberRegistration Member(Guid? category = null) => new("New", "Member", new(1990, 1, 1),
        "+27" + Random.Shared.NextInt64(100000000, 999999999), Email(), true, true, category);
    private async Task<HttpClient> Auth(string email = "tenant@example.com", string? password = null, ApiFactory? factory = null)
    {
        var client = (factory ?? fixture.Factory).CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = password ?? fixture.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken);
        return client;
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    private async Task<Guid> Create(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/admin-users", new { email, password, role = "TenantAdmin" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain(password, text); Assert.DoesNotContain("passwordHash", text); Assert.DoesNotContain("tenantId", text);
        return (await Json(response)).GetProperty("id").GetGuid();
    }
    private async Task<Tenant> BootstrapTenant()
    {
        var now = DateTimeOffset.UtcNow;
        var tenant = new Tenant { Id = Guid.NewGuid(), Code = "BOOT" + Guid.NewGuid().ToString("N"), Name = "Bootstrap test", IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now };
        await using var db = fixture.Db(); db.Tenants.Add(tenant); await db.SaveChangesAsync(); return tenant;
    }
    private async Task<Guid> Bootstrap(string code, string email, string password)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<BootstrapAdministrator>().ExecuteAsync(code, email, password, CancellationToken.None);
    }
    [Fact]
    public async Task StaffCanUseNtmButCannotManageUsersAndPromotionRevokesSessions()
    {
        using var manager = await Auth(); var email = Email(); var password = Password();
        var created = await manager.PostAsJsonAsync("/api/v1/admin-users", new { email, password });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var payload = await Json(created); Assert.Equal("TenantStaff", payload.GetProperty("role").GetString());
        var id = payload.GetProperty("id").GetGuid();
        using var staff = await Auth(email, password);
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/v1/admin/tenants/ESHL/ntm/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/admin-users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/v1/admin-users", new { email = Email(), password = Password(), role = "TenantStaff" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync($"/api/v1/admin-users/{fixture.TenantAdminId}/reset-password", new { newPassword = Password() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PutAsJsonAsync($"/api/v1/admin-users/{id}", new { email, role = "TenantAdmin" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/admin/tenants/SECOND/ntm/dashboard")).StatusCode);
        const string changedPassword = "staff-new1!";
        Assert.Equal(HttpStatusCode.NoContent, (await staff.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = password, newPassword = changedPassword, confirmPassword = changedPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.GetAsync("/api/v1/admin/tenants/ESHL/ntm/dashboard")).StatusCode);
        using var changedStaff = await Auth(email, changedPassword);
        Assert.Equal(HttpStatusCode.OK, (await changedStaff.GetAsync("/api/v1/admin/tenants/ESHL/ntm/dashboard")).StatusCode);
        Assert.Equal((HttpStatusCode)422, (await manager.PutAsJsonAsync($"/api/v1/admin-users/{id}", new { email, role = "IKhetheloAdmin" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.PutAsJsonAsync($"/api/v1/admin-users/{id}", new { email, role = "TenantAdmin" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await changedStaff.GetAsync("/api/v1/admin/tenants/ESHL/ntm/dashboard")).StatusCode);
        using var promoted = await Auth(email, changedPassword);
        Assert.Equal(HttpStatusCode.OK, (await promoted.GetAsync("/api/v1/admin-users")).StatusCode);
    }
    [Fact]
    public async Task LastActiveTenantAdministratorCannotBeDemotedWhenStaffExist()
    {
        var tenant = await BootstrapTenant(); var email = Email(); var id = await Bootstrap(tenant.Code, email, Password());
        using var scope = fixture.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ResolvedTenantContext>().Set(new(tenant.Id, tenant.Code, id, false, Guid.NewGuid().ToString()));
        var users = scope.ServiceProvider.GetRequiredService<IAdminUserManagement>();
        await users.CreateAsync(new(Email(), Password(), "TenantStaff"), CancellationToken.None);
        var demote = await Assert.ThrowsAsync<RequestFailure>(() => users.UpdateAsync(id, new(email, "TenantStaff"), CancellationToken.None));
        Assert.Equal(409, demote.Status);
        var deactivate = await Assert.ThrowsAsync<RequestFailure>(() => users.StatusAsync(id, new(false), CancellationToken.None));
        Assert.Equal(409, deactivate.Status);
    }
    [Fact]
    public async Task OwnPasswordChangeChecksCurrentPasswordRevokesTokensAndAudits()
    {
        using var manager = await Auth(); var email = Email(); var oldPassword = Password(); var id = await Create(manager, email, oldPassword);
        using var target = await Auth(email, oldPassword);
        var next = "newpass1!";
        Assert.Equal((HttpStatusCode)422, (await target.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = "wrong", newPassword = next, confirmPassword = next })).StatusCode);
        Assert.Equal((HttpStatusCode)422, (await target.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = oldPassword, newPassword = next, confirmPassword = "mismatch" })).StatusCode);
        Assert.Equal((HttpStatusCode)422, (await target.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = oldPassword, newPassword = oldPassword, confirmPassword = oldPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await target.PostAsJsonAsync($"/api/v1/admin-users/{id}/reset-password", new { newPassword = next })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await target.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = oldPassword, newPassword = next, confirmPassword = next })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await target.GetAsync("/api/v1/admin-users")).StatusCode);
        using var changed = await Auth(email, next);
        await using var db = fixture.Db(); var audit = await db.AuditLogs.SingleAsync(x => x.EntityId == id && x.Action == "ChangeOwnPassword");
        Assert.DoesNotContain(next, audit.MetadataJson ?? ""); Assert.DoesNotContain(oldPassword, audit.MetadataJson ?? "");
    }
    [Fact]
    public async Task BootstrapAcceptsEightCharacterPassword()
    {
        var tenant = await BootstrapTenant(); var email = Email();
        await Bootstrap(tenant.Code, email, "abcdef1!");
        using var client = await Auth(email, "abcdef1!");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/admin-users")).StatusCode);
    }
    [Fact]
    public async Task BootstrapCreatesHashedAuditedTenantAdminWhoCanLoginAndCannotRepeat()
    {
        var tenant = await BootstrapTenant(); var email = Email(); var password = Password();
        var id = await Bootstrap(tenant.Code, email, password);
        await using var db = fixture.Db(); var admin = await db.AdminUsers.SingleAsync(x => x.Id == id);
        Assert.Equal(tenant.Id, admin.TenantId); Assert.Equal(AdminRole.TenantAdmin, admin.Role); Assert.True(admin.IsActive);
        Assert.NotEqual(password, admin.PasswordHash);
        Assert.NotEqual(PasswordVerificationResult.Failed, new PasswordHasher<AdminUser>().VerifyHashedPassword(admin, admin.PasswordHash, password));
        Assert.True(await db.AuditLogs.AnyAsync(x => x.EntityId == id && x.Action == "BootstrapAdministrator" && x.ActorAdminUserId == id && x.TenantId == tenant.Id));
        using var client = await Auth(email, password);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/admin-users")).StatusCode);
        var failure = await Assert.ThrowsAsync<RequestFailure>(() => Bootstrap(tenant.Code, Email(), Password())); Assert.Equal(409, failure.Status);
    }
    [Fact]
    public async Task ConcurrentBootstrapCreatesExactlyOneAdministrator()
    {
        var tenant = await BootstrapTenant();
        async Task<int> Attempt()
        {
            try { await Bootstrap(tenant.Code, Email(), Password()); return 201; }
            catch (RequestFailure failure) { return failure.Status; }
        }
        var outcomes = await Task.WhenAll(Attempt(), Attempt()); Assert.Equal(new[] { 201, 409 }, outcomes.Order().ToArray());
        await using var db = fixture.Db(); Assert.Equal(1, await db.AdminUsers.CountAsync(x => x.TenantId == tenant.Id));
    }
    [Fact]
    public async Task ConcurrentDeactivationCannotRemoveTheLastActiveAdministrator()
    {
        var tenant = await BootstrapTenant(); var first = await Bootstrap(tenant.Code, Email(), Password());
        using var setup = fixture.Factory.Services.CreateScope();
        setup.ServiceProvider.GetRequiredService<ResolvedTenantContext>().Set(new(tenant.Id, tenant.Code, fixture.InternalAdminId, true, Guid.NewGuid().ToString()));
        var second = await setup.ServiceProvider.GetRequiredService<IAdminUserManagement>().CreateAsync(new(Email(), Password(), "TenantAdmin"), CancellationToken.None);
        async Task<int> Deactivate(Guid id)
        {
            using var scope = fixture.Factory.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ResolvedTenantContext>().Set(new(tenant.Id, tenant.Code, fixture.InternalAdminId, true, Guid.NewGuid().ToString()));
            try { await scope.ServiceProvider.GetRequiredService<IAdminUserManagement>().StatusAsync(id, new(false), CancellationToken.None); return 200; }
            catch (RequestFailure failure) { return failure.Status; }
        }
        var results = await Task.WhenAll(Deactivate(first), Deactivate(second.Id));
        Assert.Equal(new[] { 200, 409 }, results.Order().ToArray());
        await using var db = fixture.Db(); Assert.Equal(1, await db.AdminUsers.CountAsync(x => x.TenantId == tenant.Id && x.IsActive));
    }
    [Fact]
    public async Task BootstrapAuditFailureLeavesNoAdministrator()
    {
        var tenant = await BootstrapTenant();
        await ApiFixture.ExecuteAsync(fixture.Connection, """
            CREATE FUNCTION fail_bootstrap_audit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.action = 'BootstrapAdministrator' THEN RAISE EXCEPTION 'Audit unavailable'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER fail_bootstrap_audit BEFORE INSERT ON audit_logs FOR EACH ROW EXECUTE FUNCTION fail_bootstrap_audit();
            """);
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => Bootstrap(tenant.Code, Email(), Password()));
            await using var db = fixture.Db(); Assert.False(await db.AdminUsers.AnyAsync(x => x.TenantId == tenant.Id));
        }
        finally { await ApiFixture.ExecuteAsync(fixture.Connection, "DROP TRIGGER fail_bootstrap_audit ON audit_logs; DROP FUNCTION fail_bootstrap_audit();"); }
    }
    [Fact]
    public async Task AdministratorLifecycleSearchUpdateAndStatusAreAuditedAndTenantScoped()
    {
        using var client = await Auth(); var email = Email(); var password = Password(); var id = await Create(client, email, password);
        using var target = await Auth(email, password);
        var listed = await Json(await client.GetAsync("/api/v1/admin-users?search=" + email)); Assert.Equal(1, listed.GetProperty("totalCount").GetInt64());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/admin-users/{id}")).StatusCode);
        var changedEmail = Email();
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/v1/admin-users/{id}", new { email = changedEmail, role = "TenantAdmin" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await target.GetAsync(Tenant + "/members")).StatusCode);
        using var changed = await Auth(changedEmail, password);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/v1/admin-users/{id}/status", new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await changed.GetAsync("/api/v1/admin-users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new { email = changedEmail, password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/v1/admin-users/{id}/status", new { isActive = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await changed.GetAsync("/api/v1/admin-users")).StatusCode);
        using var reactivated = await Auth(changedEmail, password);
        Assert.Equal(HttpStatusCode.OK, (await reactivated.GetAsync("/api/v1/admin-users")).StatusCode);
        await using var db = fixture.Db(); var other = await db.AdminUsers.SingleAsync(x => x.Email == "second@example.com");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/admin-users/{other.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/v1/admin-users/{other.Id}/reset-password", new { newPassword = Password() })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsJsonAsync($"/api/v1/admin-users/{id}/reset-password", new { newPassword = Password() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin-users?tenantCode=SECOND")).StatusCode);
        Assert.True(await db.AuditLogs.AnyAsync(x => x.EntityId == id && x.Action == "UpdateAdministrator"));
        Assert.Equal(2, await db.AuditLogs.CountAsync(x => x.EntityId == id && x.Action == "SetAdministratorStatus"));
    }
    [Fact]
    public async Task PasswordResetRevokesTokensChangesHashAndNeverAuditsThePassword()
    {
        using var client = await Auth(); var email = Email(); var oldPassword = Password(); var id = await Create(client, email, oldPassword);
        using var oldClient = await Auth(email, oldPassword); var newPassword = Password();
        var reset = await client.PostAsJsonAsync($"/api/v1/admin-users/{id}/reset-password", new { newPassword });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode); Assert.True(reset.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.Unauthorized, (await oldClient.GetAsync(Tenant + "/members")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = oldPassword })).StatusCode);
        using var newClient = await Auth(email, newPassword); Assert.Equal(HttpStatusCode.OK, (await newClient.GetAsync(Tenant + "/members")).StatusCode);
        await using var db = fixture.Db(); var audit = await db.AuditLogs.SingleAsync(x => x.EntityId == id && x.Action == "ResetAdministratorPassword");
        Assert.DoesNotContain(newPassword, audit.MetadataJson ?? ""); Assert.DoesNotContain(oldPassword, audit.MetadataJson ?? "");
    }
    [Fact]
    public async Task AdministratorCreationRejectsEscalationWeakPasswordsOwnershipAndDuplicateEmails()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/v1/admin-users")).StatusCode);
        using var client = await Auth();
        Assert.Equal((HttpStatusCode)422, (await client.PostAsJsonAsync("/api/v1/admin-users", new { email = Email(), password = Password(), role = "IKhetheloAdmin" })).StatusCode);
        Assert.Equal((HttpStatusCode)422, (await client.PostAsJsonAsync("/api/v1/admin-users", new { email = Email(), password = "short", role = "TenantAdmin" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/admin-users", new { email = Email(), password = Password(), role = "TenantAdmin", tenantId = fixture.Second })).StatusCode);
        var email = Email(); var id = await Create(client, email, Password());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/v1/admin-users", new { email = email.ToUpperInvariant(), password = Password(), role = "TenantAdmin" })).StatusCode);
        Assert.Equal((HttpStatusCode)422, (await client.PutAsJsonAsync($"/api/v1/admin-users/{id}", new { email, role = "IKhetheloAdmin" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PatchAsJsonAsync($"/api/v1/admin-users/{fixture.TenantAdminId}/status", new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await client.PatchAsync($"/api/v1/admin-users/{id}/status", new StringContent(new string(' ', 17000), Encoding.UTF8, "application/json"))).StatusCode);
    }
    [Fact]
    public async Task InternalMembershipGrantDoesNotGrantAdministratorManagementAndLastAdminIsProtected()
    {
        using var denied = await Auth("internal@example.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync("/api/v1/admin-users?tenantCode=SECOND")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await denied.GetAsync("/api/v1/admin-users")).StatusCode);
        await using var factory = new ApiFactory(fixture.Connection, fixture.PrivateKey, internalAdmin: fixture.InternalAdminId, manageAdmins: true);
        using var manager = await Auth("internal@example.com", factory: factory);
        await using var db = fixture.Db(); var only = await db.AdminUsers.SingleAsync(x => x.Email == "second@example.com");
        var status = await manager.PatchAsJsonAsync($"/api/v1/admin-users/{only.Id}/status?tenantCode=SECOND", new { isActive = false });
        Assert.Equal(HttpStatusCode.Conflict, status.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/v1/admin-users?tenantCode=SECOND")).StatusCode);
        Assert.True(await db.AuditLogs.AnyAsync(x => x.Action == "CrossTenantRead" && x.EntityType == "AdminUser" && x.TenantId == fixture.Second && x.ActorAdminUserId == fixture.InternalAdminId));
    }
    [Fact]
    public async Task MemberRegistrationCategoriesStatusAndHistoryWorkWithoutChangingExistingDto()
    {
        using var client = await Auth();
        var created = await client.PostAsJsonAsync(Tenant + "/membership-categories", new { name = "  Standard  ", isActive = true });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); var category = (await Json(created)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"{Tenant}/membership-categories/{category}", new { name = "Standard " + category.ToString("N"), isActive = true })).StatusCode);
        var categories = await Json(await client.GetAsync(Tenant + "/membership-categories?search=" + category.ToString("N") + "&status=Active"));
        Assert.Equal(1, categories.GetProperty("totalCount").GetInt64());
        var registration = Member(category); var response = await client.PostAsJsonAsync(Tenant + "/members", registration);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); var member = await Json(response); var id = member.GetProperty("memberId").GetGuid();
        Assert.False(member.TryGetProperty("tenantId", out _)); Assert.Matches("^ESC-", member.GetProperty("memberNumber").GetString()!);
        var assignment = await Json(await client.GetAsync($"{Tenant}/members/{id}/category")); Assert.Equal(category, assignment.GetProperty("categoryId").GetGuid());
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"{Tenant}/members/{id}/status", new { status = "Suspended" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"{Tenant}/members/{id}/category", new { categoryId = (Guid?)null })).StatusCode);
        var history = await Json(await client.GetAsync($"{Tenant}/members/{id}/history")); var events = history.GetProperty("items").EnumerateArray().ToArray();
        Assert.Contains(events, x => x.GetProperty("action").GetString() == "RegisterMember");
        var change = Assert.Single(events, x => x.GetProperty("action").GetString() == "UpdateMemberStatus");
        Assert.Equal("Active", change.GetProperty("previousStatus").GetString()); Assert.Equal("Suspended", change.GetProperty("status").GetString());
        Assert.Contains(events, x => x.GetProperty("action").GetString() == "SetMemberCategory");
        await using var db = fixture.Db(); var application = await db.Applications.SingleAsync(x => x.Id == member.GetProperty("applicationId").GetGuid());
        Assert.Equal(MembershipApplicationStatus.Completed, application.Status); Assert.Equal(fixture.TenantAdminId, application.ReviewedByAdminId);
    }
    [Fact]
    public async Task CategoriesAndHistoryCannotCrossTenantsAndInactiveCategoriesCannotBeAssigned()
    {
        using var client = await Auth(); using var other = await Auth("second@example.com");
        var uniqueName = "Category " + Guid.NewGuid().ToString("N");
        var response = await other.PostAsJsonAsync("/api/v1/admin/tenants/SECOND/membership-categories", new { name = uniqueName, isActive = true });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); var category = (await Json(response)).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(Tenant + "/members", Member(category))).StatusCode);
        var member = await client.PostAsJsonAsync(Tenant + "/members", Member()); var id = (await Json(member)).GetProperty("memberId").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"{Tenant}/members/{id}/category", new { categoryId = category })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/admin/tenants/SECOND/members/{id}/history")).StatusCode);
        var inactive = await client.PostAsJsonAsync(Tenant + "/membership-categories", new { name = uniqueName, isActive = false });
        var inactiveId = (await Json(inactive)).GetProperty("id").GetGuid();
        Assert.Equal((HttpStatusCode)422, (await client.PutAsJsonAsync($"{Tenant}/members/{id}/category", new { categoryId = inactiveId })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Tenant + "/membership-categories", new { name = uniqueName.ToUpperInvariant(), isActive = true })).StatusCode);
    }
    [Fact]
    public async Task DirectRegistrationValidatesConsentAndDoesNotBypassPendingOrActiveIdentityConflicts()
    {
        using var client = await Auth(); var request = Member();
        Assert.Equal((HttpStatusCode)422, (await client.PostAsJsonAsync(Tenant + "/members", request with { TermsAccepted = false })).StatusCode);
        var submitted = await fixture.Client.PostAsJsonAsync("/api/v1/public/eshlahleni/membership-applications",
            new ApplicationSubmission(request.FirstName, request.LastName, request.DateOfBirth, request.PhoneNumber, request.Email, true, true));
        Assert.Equal(HttpStatusCode.Created, submitted.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Tenant + "/members", request)).StatusCode);
        await using var db = fixture.Db(); Assert.False(await db.Members.AnyAsync(x => x.Email == request.Email));
        var fresh = Member(); Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(Tenant + "/members", fresh)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Tenant + "/members", fresh)).StatusCode);
    }
    [Fact]
    public async Task RequiredAuditFailureRollsBackAdministratorAndMemberCreation()
    {
        using var client = await Auth(); var email = Email(); var registration = Member();
        await ApiFixture.ExecuteAsync(fixture.Connection, """
            CREATE FUNCTION fail_administration_audit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.action IN ('CreateAdministrator', 'RegisterMember') THEN RAISE EXCEPTION 'Audit unavailable'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER fail_administration_audit BEFORE INSERT ON audit_logs FOR EACH ROW EXECUTE FUNCTION fail_administration_audit();
            """);
        try
        {
            Assert.Equal(HttpStatusCode.InternalServerError, (await client.PostAsJsonAsync("/api/v1/admin-users", new { email, password = Password(), role = "TenantAdmin" })).StatusCode);
            Assert.Equal(HttpStatusCode.InternalServerError, (await client.PostAsJsonAsync(Tenant + "/members", registration)).StatusCode);
            await using var db = fixture.Db(); Assert.False(await db.AdminUsers.AnyAsync(x => x.Email == email));
            Assert.False(await db.Applications.AnyAsync(x => x.Email == registration.Email)); Assert.False(await db.Members.AnyAsync(x => x.Email == registration.Email));
        }
        finally { await ApiFixture.ExecuteAsync(fixture.Connection, "DROP TRIGGER fail_administration_audit ON audit_logs; DROP FUNCTION fail_administration_audit();"); }
    }
}
