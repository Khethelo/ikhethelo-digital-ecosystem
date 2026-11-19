using System.Net;
using System.Net.Http.Json;
using IKhethelo.Digital.Application;
using IKhethelo.Digital.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace IKhethelo.Digital.Tests;

[Collection("PostgreSQL")]
public sealed class NtmIntegrationTests(ApiFixture fixture)
{
    private const string Root = "/api/v1/admin/tenants/SECOND/ntm";
    private async Task<HttpClient> Auth(string email = "second@example.com", ApiFactory? factory = null)
    {
        var client = (factory ?? fixture.Factory).CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = fixture.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken);
        return client;
    }
    private static string Number() => Guid.NewGuid().ToString("N");
    private static async Task<NtmVeteranResponse> Veteran(HttpClient client, string? mkmva = null, string? dmv = null, string root = Root)
    {
        var response = await client.PostAsJsonAsync(root + "/veterans", new NtmVeteranWrite("Veteran " + Number(), mkmva, dmv));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<NtmVeteranResponse>())!;
    }
    [Fact]
    public async Task IdentifierValidationNormalizedDuplicatesAndUpdates()
    {
        using var client = await Auth(); var number = Number();
        var first = await Veteran(client, mkmva: number); Assert.Null(first.Business);
        var dmv = await Veteran(client, dmv: Number()); Assert.Null(dmv.MkmvaNumber);
        Assert.Equal((HttpStatusCode)422, (await client.PostAsJsonAsync(Root + "/veterans", new NtmVeteranWrite("Missing"))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Root + "/veterans", new NtmVeteranWrite("Duplicate", " " + number.ToUpperInvariant() + " "))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(Root + $"/veterans/{dmv.Id}", new NtmVeteranWrite("Duplicate", number))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Root + "/veterans", new NtmVeteranWrite("Duplicate DMV", DmvNumber: dmv.DmvNumber!.ToUpperInvariant()))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(Root + $"/veterans/{first.Id}", new NtmVeteranWrite("Updated", number))).StatusCode);
    }
    [Fact]
    public async Task ConcurrentSecondBusinessReturnsConflictAndAuditIsAtomic()
    {
        using var client = await Auth(); var veteran = await Veteran(client, Number());
        var request = new NtmBusinessWrite(veteran.Id, "Business", CurrentBusinessNeeds: "Equipment and training descriptions");
        var responses = await Task.WhenAll(client.PostAsJsonAsync(Root + "/businesses", request), client.PostAsJsonAsync(Root + "/businesses", request));
        Assert.Equal(new[] { 201, 409 }, responses.Select(x => (int)x.StatusCode).Order());
        var business = (await responses.Single(x => x.StatusCode == HttpStatusCode.Created).Content.ReadFromJsonAsync<NtmBusinessResponse>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(Root + "/businesses", request)).StatusCode);
        var detail = await client.GetFromJsonAsync<NtmVeteranResponse>(Root + $"/veterans/{veteran.Id}");
        Assert.Equal(business.Id, detail!.Business!.Id);
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(Root + $"/businesses/{business.Id}", request with { BusinessName = "Changed" })).StatusCode);
        await using var db = fixture.Db();
        Assert.Equal(1, await db.NtmBusinesses.CountAsync(x => x.TenantId == fixture.Second && x.VeteranId == veteran.Id));
        foreach (var (id, action) in new[] { (veteran.Id, "NtmVeteranCreated"), (business.Id, "NtmBusinessCreated"), (business.Id, "NtmBusinessUpdated") })
        {
            var audit = await db.AuditLogs.SingleAsync(x => x.EntityId == id && x.Action == action);
            Assert.Equal(fixture.Second, audit.TenantId); Assert.NotNull(audit.ActorAdminUserId);
            Assert.DoesNotContain("Equipment", audit.MetadataJson ?? "");
        }
    }
    [Fact]
    public async Task AllRoutesRequireAuthenticationTenantOwnershipAndExplicitInternalGrant()
    {
        using var client = await Auth(); var veteran = await Veteran(client, Number());
        var created = await client.PostAsJsonAsync(Root + "/businesses", new NtmBusinessWrite(veteran.Id, "Private business"));
        var business = (await created.Content.ReadFromJsonAsync<NtmBusinessResponse>())!;
        foreach (var suffix in new[] { "/dashboard", "/veterans", $"/veterans/{veteran.Id}", "/businesses", $"/businesses/{business.Id}" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync(Root + suffix)).StatusCode);
        using var eshl = await Auth("tenant@example.com");
        foreach (var suffix in new[] { "/dashboard", "/veterans", $"/veterans/{veteran.Id}", "/businesses", $"/businesses/{business.Id}" })
            Assert.Equal(HttpStatusCode.Forbidden, (await eshl.GetAsync(Root + suffix)).StatusCode);
        const string otherRoot = "/api/v1/admin/tenants/ESHL/ntm";
        Assert.Equal(HttpStatusCode.NotFound, (await eshl.GetAsync(otherRoot + $"/veterans/{veteran.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await eshl.GetAsync(otherRoot + $"/businesses/{business.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await eshl.PutAsJsonAsync(otherRoot + $"/veterans/{veteran.Id}", new NtmVeteranWrite("No access", Number()))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await eshl.PutAsJsonAsync(otherRoot + $"/businesses/{business.Id}", new NtmBusinessWrite(veteran.Id, "No access"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await eshl.PostAsJsonAsync(otherRoot + "/businesses", new NtmBusinessWrite(veteran.Id, "Cross tenant"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(Root + "/veterans", new { fullName = "Tamper", mkmvaNumber = Number(), tenantId = fixture.Eshl })).StatusCode);
        await using var deniedFactory = new ApiFactory(fixture.Connection, fixture.PrivateKey);
        using var denied = await Auth("internal@example.com", deniedFactory);
        Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync(Root + "/dashboard")).StatusCode);
        using var allowed = await Auth("internal@example.com");
        Assert.Equal(HttpStatusCode.OK, (await allowed.GetAsync(Root + "/dashboard")).StatusCode);
        await using var db = fixture.Db();
        Assert.True(await db.AuditLogs.AnyAsync(x => x.TenantId == fixture.Second && x.EntityType == "NtmDashboard" && x.Action == "CrossTenantRead"));
    }
    [Fact]
    public async Task DatabaseRejectsCrossTenantReferencesAndDuplicateAssignmentsDirectly()
    {
        using var client = await Auth(); var veteran = await Veteran(client, Number());
        await using var db = fixture.Db(); var now = DateTimeOffset.UtcNow;
        db.NtmBusinesses.Add(new NtmBusiness { Id = Guid.NewGuid(), TenantId = fixture.Eshl, VeteranId = veteran.Id, BusinessName = "Forbidden", CreatedAtUtc = now, UpdatedAtUtc = now });
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ((PostgresException)failure.InnerException!).SqlState);
        db.ChangeTracker.Clear();
        foreach (var name in new[] { "First", "Second" })
            db.NtmBusinesses.Add(new NtmBusiness { Id = Guid.NewGuid(), TenantId = fixture.Second, VeteranId = veteran.Id, BusinessName = name, CreatedAtUtc = now, UpdatedAtUtc = now });
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)duplicate.InnerException!).SqlState);
    }
    [Fact]
    public async Task DashboardAndFiltersReflectOnlyRecordedSameTenantData()
    {
        using var client = await Auth(); var before = (await client.GetFromJsonAsync<NtmDashboard>(Root + "/dashboard"))!;
        var sector = "Sector-" + Number(); var need = "Training-" + Number(); var ids = new List<Guid>();
        foreach (var status in NtmValidation.OperationalStatuses)
        {
            var veteran = await Veteran(client, Number()); ids.Add(veteran.Id);
            var response = await client.PostAsJsonAsync(Root + "/businesses", new NtmBusinessWrite(veteran.Id, "Findable " + sector,
                Sector: sector, OperationalStatus: status, PriorityNeed: need, FundingRequired: true, FundingAmount: 12.34m));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        var after = (await client.GetFromJsonAsync<NtmDashboard>(Root + "/dashboard"))!;
        Assert.Equal(before.TotalVeterans + 3, after.TotalVeterans); Assert.Equal(before.TotalBusinesses + 3, after.TotalBusinesses);
        Assert.Equal(before.OperationalBusinesses + 1, after.OperationalBusinesses);
        Assert.Equal(before.PartiallyOperationalBusinesses + 1, after.PartiallyOperationalBusinesses);
        Assert.Equal(before.NotOperationalBusinesses + 1, after.NotOperationalBusinesses);
        Assert.Equal(before.TotalRecordedFundingAmount + 37.02m, after.TotalRecordedFundingAmount);
        Assert.Equal(3, after.BusinessesBySector.Single(x => x.Value == sector).Count);
        Assert.Equal(3, after.RecordedPriorityNeeds.Single(x => x.Value == need).Count);
        var page = await client.GetFromJsonAsync<Page<NtmBusinessResponse>>(Root + "/businesses?sector=" + sector + "&pageSize=1");
        Assert.Equal(3, page!.TotalCount); Assert.Single(page.Items);
        var filtered = await client.GetFromJsonAsync<Page<NtmBusinessResponse>>(Root + "/businesses?sector=" + sector + "&operationalStatus=Operational&veteranId=" + ids[0]);
        Assert.Single(filtered!.Items);
        using var eshl = await Auth("tenant@example.com");
        var isolated = await eshl.GetFromJsonAsync<Page<NtmBusinessResponse>>("/api/v1/admin/tenants/ESHL/ntm/businesses?sector=" + sector);
        Assert.Empty(isolated!.Items);
    }
    [Fact]
    public async Task BusinessUpdatesPreserveHistoricalSourceFieldsWithoutReturningThem()
    {
        using var client = await Auth();
        var veteran = await Veteran(client, Number());
        var response = await client.PostAsJsonAsync(Root + "/businesses", new NtmBusinessWrite(veteran.Id, "Business"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<NtmBusinessResponse>())!;
        await using (var db = fixture.Db())
        {
            var row = await db.NtmBusinesses.SingleAsync(x => x.Id == created.Id);
            row.SourceRecordNumber = 1; row.OperationalStatusSource = " Operationl ";
            row.NumberOfEmployeesSource = "3 part time"; row.YearsInOperationSource = "Needs training";
            row.FundingRequiredSource = "Funding";
            await db.SaveChangesAsync();
        }
        var update = await client.PutAsJsonAsync(Root + $"/businesses/{created.Id}", new NtmBusinessWrite(veteran.Id, "Updated"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var json = await update.Content.ReadAsStringAsync();
        Assert.DoesNotContain("operationalStatusSource", json);
        Assert.DoesNotContain("sourceRecordNumber", await client.GetStringAsync(Root + $"/businesses/{created.Id}"));
        await using var verify = fixture.Db();
        var stored = await verify.NtmBusinesses.SingleAsync(x => x.Id == created.Id);
        Assert.Equal("Updated", stored.BusinessName); Assert.Equal(1, stored.SourceRecordNumber);
        Assert.Equal(" Operationl ", stored.OperationalStatusSource); Assert.Equal("3 part time", stored.NumberOfEmployeesSource);
        Assert.Equal("Needs training", stored.YearsInOperationSource); Assert.Equal("Funding", stored.FundingRequiredSource);
    }
    [Fact]
    public async Task InvalidBusinessValuesAreRejectedAtTheApiBoundary()
    {
        using var client = await Auth(); var veteran = await Veteran(client, Number());
        foreach (var request in new[] {
            new NtmBusinessWrite(veteran.Id, "Invalid status", OperationalStatus: "Invented"),
            new NtmBusinessWrite(veteran.Id, "Invalid employees", NumberOfEmployees: -1),
            new NtmBusinessWrite(veteran.Id, "Invalid funding", FundingAmount: -1),
            new NtmBusinessWrite(veteran.Id, "Invalid years", YearsInOperation: -1) })
            Assert.Equal((HttpStatusCode)422, (await client.PostAsJsonAsync(Root + "/businesses", request)).StatusCode);
        await using var db = fixture.Db(); Assert.False(await db.NtmBusinesses.AnyAsync(x => x.VeteranId == veteran.Id));
    }
    [Fact]
    public async Task AuditFailureRollsBackVeteranWrite()
    {
        using var client = await Auth(); var number = Number();
        await ApiFixture.ExecuteAsync(fixture.Connection, """
            CREATE FUNCTION fail_ntm_audit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.action = 'NtmVeteranCreated' THEN RAISE EXCEPTION 'Audit unavailable'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER fail_ntm_audit BEFORE INSERT ON audit_logs FOR EACH ROW EXECUTE FUNCTION fail_ntm_audit();
            """);
        try
        {
            Assert.Equal(HttpStatusCode.InternalServerError, (await client.PostAsJsonAsync(Root + "/veterans", new NtmVeteranWrite("Rollback", number))).StatusCode);
            await using var db = fixture.Db(); Assert.False(await db.NtmVeterans.AnyAsync(x => x.MkmvaNumber == number));
        }
        finally { await ApiFixture.ExecuteAsync(fixture.Connection, "DROP TRIGGER fail_ntm_audit ON audit_logs; DROP FUNCTION fail_ntm_audit();"); }
    }
}
