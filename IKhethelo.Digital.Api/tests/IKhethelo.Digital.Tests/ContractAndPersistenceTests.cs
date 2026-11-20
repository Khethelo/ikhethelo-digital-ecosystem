using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IKhethelo.Digital.Application;
using IKhethelo.Digital.Domain;
using IKhethelo.Digital.Infrastructure.Persistence;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Readers;
using Npgsql;
using Xunit;

namespace IKhethelo.Digital.Tests;

[Collection("PostgreSQL")]
public sealed class ContractAndPersistenceTests(ApiFixture fixture)
{
    [Fact]
    public async Task DevelopmentSwaggerServesValidAuthoritativeContractAndBearerAuthorize()
    {
        var response = await fixture.Client.GetAsync("/swagger/index.html"); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(); Assert.Contains("swagger-ui", html);
        var yaml = await fixture.Client.GetStringAsync("/swagger/contract.yaml");
        var contract = new OpenApiStringReader().Read(yaml, out var diagnostic);
        Assert.Empty(diagnostic.Errors); Assert.Equal("1.2.0", contract.Info.Version);
        Assert.Equal("bearer", contract.Components.SecuritySchemes["BearerAuth"].Scheme);
        var expectedRoutes = contract.Paths.SelectMany(path => path.Value.Operations.Select(operation => operation.Key.ToString().ToUpperInvariant() + " " + path.Key)).Order().ToArray();
        var endpoints = fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();
        var actualRoutes = endpoints.Where(x => x.RoutePattern.RawText?.StartsWith("api/v1", StringComparison.Ordinal) == true)
            .SelectMany(x => x.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Select(method => method + " /" + x.RoutePattern.RawText)).Order().ToArray();
        Assert.Equal(expectedRoutes, actualRoutes);
        Assert.Empty(contract.Paths["/api/v1/auth/login"].Operations[Microsoft.OpenApi.Models.OperationType.Post].Security);
        Assert.Single(contract.SecurityRequirements);
        foreach (var (schema, dto) in new (string, Type)[] {
            ("ApplicationSubmission", typeof(ApplicationSubmission)), ("Application", typeof(ApplicationResponse)),
            ("Member", typeof(MemberResponse)), ("MemberUpdate", typeof(MemberUpdate)),
            ("MemberStatusUpdate", typeof(MemberStatusUpdate)), ("ReviewRequest", typeof(ReviewRequest)),
            ("LoginRequest", typeof(LoginRequest)), ("TokenResponse", typeof(TokenResponse)),
            ("SubmissionReceipt", typeof(SubmissionReceipt)), ("ApplicationPage", typeof(Page<ApplicationResponse>)),
            ("AdminCreate", typeof(AdminCreate)), ("AdminUpdate", typeof(AdminUpdate)), ("Administrator", typeof(AdminResponse)),
            ("AdminStatusUpdate", typeof(AdminStatusUpdate)), ("AdminPasswordReset", typeof(AdminPasswordReset)),
            ("MemberRegistration", typeof(MemberRegistration)), ("CategoryWrite", typeof(CategoryWrite)),
            ("MembershipCategory", typeof(CategoryResponse)), ("MemberCategoryUpdate", typeof(MemberCategoryUpdate)),
            ("MemberCategory", typeof(MemberCategoryResponse)), ("MemberHistoryEntry", typeof(MemberHistoryEntry)),
            ("NtmVeteranWrite", typeof(NtmVeteranWrite)), ("NtmVeteran", typeof(NtmVeteranResponse)),
            ("NtmBusinessWrite", typeof(NtmBusinessWrite)), ("NtmBusiness", typeof(NtmBusinessResponse)),
            ("NtmVeteranPage", typeof(Page<NtmVeteranResponse>)), ("NtmBusinessPage", typeof(Page<NtmBusinessResponse>)),
            ("NtmDashboard", typeof(NtmDashboard)) })
        {
            var properties = dto.GetProperties().Select(property =>
                property.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonPropertyNameAttribute), false)
                    .Cast<System.Text.Json.Serialization.JsonPropertyNameAttribute>().SingleOrDefault()?.Name
                    ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name)).Order().ToArray();
            Assert.Equal(contract.Components.Schemas[schema].Properties.Keys.Order().ToArray(), properties);
        }
    }
    [Fact]
    public async Task SwaggerIsNotAvailableInProduction()
    {
        await using var factory = new ApiFactory(fixture.Connection, fixture.PrivateKey, "Production");
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/swagger/index.html")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/swagger/contract.yaml")).StatusCode);
    }
    [Fact]
    public async Task MigratedPostgresCatalogMatchesBaselineExactly()
    {
        var baseline = await CatalogAsync(fixture.BaselineConnection); var migrated = await CatalogAsync(fixture.Connection);
        Assert.Equal(baseline, migrated);
        await using var db = fixture.Db(); Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(5, (await db.Database.GetAppliedMigrationsAsync()).Count());
    }
    private static async Task<string[]> CatalogAsync(string connection)
    {
        // Use the server's own canonical definitions to compare semantic DDL, including defaults and partial predicates.
        const string sql = """
            SELECT 'column|' || table_name || '|' || column_name || '|' || data_type || '|' ||
                coalesce(character_maximum_length::text,'') || '|' || is_nullable || '|' || coalesce(column_default,'') AS definition
            FROM information_schema.columns WHERE table_schema='public' AND table_name <> '__EFMigrationsHistory'
            UNION ALL
            SELECT 'constraint|' || c.relname || '|' || con.conname || '|' || pg_get_constraintdef(con.oid)
            FROM pg_constraint con JOIN pg_class c ON c.oid=con.conrelid JOIN pg_namespace n ON n.oid=c.relnamespace
            WHERE n.nspname='public' AND c.relname <> '__EFMigrationsHistory'
            UNION ALL
            SELECT 'index|' || tablename || '|' || indexname || '|' || indexdef FROM pg_indexes
            WHERE schemaname='public' AND tablename <> '__EFMigrationsHistory'
            UNION ALL
            SELECT 'sequence|' || sequencename || '|' || data_type || '|' || start_value || '|' || min_value || '|' || max_value || '|' || increment_by || '|' || cycle || '|' || cache_size
            FROM pg_sequences WHERE schemaname='public'
            ORDER BY definition
            """;
        await using var conn = new NpgsqlConnection(connection); await conn.OpenAsync();
        await using var command = new NpgsqlCommand(sql, conn); await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>(); while (await reader.ReadAsync()) rows.Add(reader.GetString(0)); return rows.ToArray();
    }
    [Fact]
    public async Task TenantSeedIsIdempotentAndModelHasOnlyApprovedEntities()
    {
        var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "baseline.sql"));
        var seed = sql[sql.IndexOf("INSERT INTO tenants", StringComparison.Ordinal)..sql.IndexOf("COMMIT;", StringComparison.Ordinal)];
        await ApiFixture.ExecuteAsync(fixture.Connection, seed); await ApiFixture.ExecuteAsync(fixture.Connection, seed);
        await using var db = fixture.Db(); var rows = await db.Tenants.Where(x => x.Code == "ESHL").ToListAsync(); Assert.Single(rows);
        Assert.Equal(fixture.Eshl, rows[0].Id); Assert.True(rows[0].IsActive); Assert.Equal("Es'hlahleni Social Club", rows[0].Name);
        Assert.Equal(new[] { "admin_users", "audit_logs", "members", "membership_applications", "membership_categories", "ntm_businesses", "ntm_veterans", "tenants" },
            db.Model.GetEntityTypes().Select(x => x.GetTableName()!).Order().ToArray());
    }
    [Fact]
    public async Task DatabaseChecksEnforceConsentNamesStatusAndReviewConsistency()
    {
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/public/eshlahleni/membership-applications", NewSubmission());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); var id = (await JsonAsync(response)).GetProperty("applicationId").GetGuid();
        foreach (var (set, constraint) in new[] {
            ("terms_accepted=false", "ck_membership_applications_terms"), ("privacy_consent=false", "ck_membership_applications_privacy"),
            ("first_name='A'", "ck_membership_applications_names"), ("status='Unsupported'", "ck_membership_applications_status"),
            ("status='Approved'", "ck_membership_applications_review_state"), ("date_of_birth=CURRENT_DATE + 1", "ck_membership_applications_dob") })
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(() => ApiFixture.ExecuteAsync(fixture.Connection, $"UPDATE membership_applications SET {set} WHERE id='{id}'"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
            if (constraint == "ck_membership_applications_status")
                Assert.Contains(exception.ConstraintName, new[] { "ck_membership_applications_status", "ck_membership_applications_review_state" });
            else Assert.Equal(constraint, exception.ConstraintName);
        }
    }
    [Fact]
    public async Task RequiredAuditFailureRollsBackApprovalAndFailsInternalRead()
    {
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/public/eshlahleni/membership-applications", NewSubmission());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); var id = (await JsonAsync(response)).GetProperty("applicationId").GetGuid();
        // Test-only fault injection into this fixture's disposable database; it is not part of the EF model.
        await ApiFixture.ExecuteAsync(fixture.Connection, $"""
            CREATE FUNCTION fail_required_audit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.entity_id = '{id}'::uuid THEN RAISE EXCEPTION 'Required audit unavailable'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER fail_required_audit BEFORE INSERT ON audit_logs FOR EACH ROW EXECUTE FUNCTION fail_required_audit();
            """);
        try
        {
            using var client = fixture.Factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
            var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "internal@example.com", password = fixture.Password });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            client.DefaultRequestHeaders.Authorization = new("Bearer", (await login.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken);
            var path = $"/api/v1/admin/tenants/eshlahleni/membership-applications/{id}";
            Assert.Equal(HttpStatusCode.InternalServerError, (await client.PostAsJsonAsync(path + "/activate", new { })).StatusCode);
            Assert.Equal(HttpStatusCode.InternalServerError, (await client.GetAsync(path)).StatusCode);
            await using var db = fixture.Db(); var row = await db.Applications.SingleAsync(x => x.Id == id);
            Assert.Equal(MembershipApplicationStatus.Pending, row.Status); Assert.Null(row.ReviewedAtUtc); Assert.Null(row.ReviewedByAdminId);
            Assert.False(await db.Members.AnyAsync(x => x.ApplicationId == id));
            Assert.False(await db.AuditLogs.AnyAsync(x => x.EntityId == id && x.Action != "RegistrationCreated"));
            Assert.Single(await db.AuditLogs.Where(x => x.EntityId == id && x.Action == "RegistrationCreated").ToListAsync());
        }
        finally { await ApiFixture.ExecuteAsync(fixture.Connection, "DROP TRIGGER fail_required_audit ON audit_logs; DROP FUNCTION fail_required_audit();"); }
    }
    private static ApplicationSubmission NewSubmission() => new("First", "Last", new(1990, 1, 1), "+27" + Random.Shared.NextInt64(100000000, 999999999), Guid.NewGuid().ToString("N") + "@example.com", true, true);
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response) => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
}
