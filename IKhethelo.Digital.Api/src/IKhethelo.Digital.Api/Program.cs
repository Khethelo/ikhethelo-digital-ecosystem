using System.Security.Claims;
using System.Net;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using IKhethelo.Digital.Api;
using IKhethelo.Digital.Api.Http;
using IKhethelo.Digital.Api.Tenancy;
using IKhethelo.Digital.Application;
using IKhethelo.Digital.Domain;
using IKhethelo.Digital.Infrastructure.Audit;
using IKhethelo.Digital.Infrastructure.Identity;
using IKhethelo.Digital.Infrastructure.Membership;
using IKhethelo.Digital.Infrastructure.Persistence;
using IKhethelo.Digital.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var bootstrap = args.Contains("--bootstrap-admin", StringComparer.Ordinal);
var adoptDatabase = args.Contains("--adopt-existing-database", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(x => x is not ("--bootstrap-admin" or "--adopt-existing-database")).ToArray());
var renderPort = builder.Configuration["PORT"];
if (!string.IsNullOrEmpty(renderPort))
{
    if (!int.TryParse(renderPort, out var port) || port is < 1 or > 65535)
        throw new InvalidOperationException("PORT must be a valid TCP port.");
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    foreach (var proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [])
        options.KnownProxies.Add(IPAddress.Parse(proxy));
    foreach (var network in builder.Configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? [])
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
});
builder.Logging.ClearProviders();
// dotnet watch discovers listening URLs from console output. JSON log lines can
// leave trailing JSON in the detected browser URL, so use plain logs locally.
if (builder.Environment.IsDevelopment())
    builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
else
    builder.Logging.AddJsonConsole();
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 16384);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHealthChecks();
builder.Services.AddDbContext<DigitalDbContext>((services, options) => options.UseNpgsql(
    services.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Configure ConnectionStrings:DefaultConnection.")));
builder.Services.AddSingleton(services => services.GetRequiredService<IConfiguration>().GetSection("Jwt").Get<JwtSettings>() ?? new());
builder.Services.AddSingleton<JwtKeys>();
builder.Services.AddSingleton<ITokenIssuer, JwtTokens>();
builder.Services.AddSingleton<IPasswordHasher<AdminUser>, PasswordHasher<AdminUser>>();
builder.Services.AddSingleton<PasswordTimingGuard>();
builder.Services.AddScoped<ResolvedTenantContext>();
builder.Services.AddScoped<ITenantContext>(services => services.GetRequiredService<ResolvedTenantContext>());
builder.Services.AddSingleton(services => services.GetRequiredService<IConfiguration>().GetSection("Tenancy").Get<TenantRouting>() ?? new());
builder.Services.AddScoped<TenantData>(); builder.Services.AddScoped<AuditWriter>();
builder.Services.AddSingleton(services => services.GetRequiredService<IConfiguration>().GetSection("Membership").Get<MembershipRules>() ?? new());
builder.Services.AddScoped<ISubmitMembershipApplication, SubmitMembershipApplication>();
builder.Services.AddScoped<IMembershipQueries, MembershipQueries>();
builder.Services.AddScoped<IReviewApplications, ReviewApplications>();
builder.Services.AddOptions<EmailSettings>().BindConfiguration("Email")
    .Validate(s => !s.Enabled || (!string.IsNullOrWhiteSpace(s.Host) && s.Port is > 0 and <= 65535 &&
        System.Net.Mail.MailAddress.TryCreate(s.FromAddress, out _) && !string.IsNullOrWhiteSpace(s.Username) && !string.IsNullOrWhiteSpace(s.Password)),
        "Configure Email host, port, sender, username and password when enabled.").ValidateOnStart();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<MembershipEmails>();
builder.Services.AddScoped<IMemberAdministration, MemberAdministration>();
builder.Services.AddScoped<IAdministratorAuthentication, AdministratorAuthentication>();
builder.Services.AddScoped<IAdminUserManagement, AdminUserManagement>();
builder.Services.AddScoped<BootstrapAdministrator>();
builder.Services.AddScoped<ExistingDatabaseAdoption>();
builder.Services.AddScoped<IMemberOperations, MemberOperations>();
builder.Services.AddScoped<INtmOperations, IKhethelo.Digital.Infrastructure.Ntm.NtmOperations>();
builder.Services.AddScoped<IAuthorizationHandler, NtmAccessHandler>();
builder.Services.AddScoped<IAuthorizationHandler, TenantPolicyHandler>();
builder.Services.AddScoped<IAuthorizationHandler, AdministratorManagementHandler>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<JwtSettings, JwtKeys>((options, settings, keys) =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = settings.Issuer, ValidateAudience = true, ValidAudience = settings.Audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = keys.Key, ValidateLifetime = true, RequireExpirationTime = true,
        RequireSignedTokens = true, ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = "sub", RoleClaimType = "role"
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var principal = context.Principal!;
            if (principal.FindAll("sub").Count() != 1 || principal.FindAll("role").Count() != 1 ||
                !Guid.TryParse(principal.FindFirstValue("sub"), out var id)) { context.Fail("Invalid identity."); return; }
            var db = context.HttpContext.RequestServices.GetRequiredService<DigitalDbContext>();
            var admin = await db.AdminUsers.AsNoTracking().Include(x => x.Tenant).SingleOrDefaultAsync(x => x.Id == id, context.HttpContext.RequestAborted);
            if (admin is null || !admin.IsActive || principal.FindFirstValue("role") != admin.Role.ToString() ||
                principal.FindAll("security_version").Count() > 1 ||
                !int.TryParse(principal.FindFirstValue("security_version") ?? "0", out var securityVersion) || securityVersion != admin.SecurityVersion ||
                !Enum.IsDefined(admin.Role) ||
                (admin.Role == AdminRole.TenantAdmin && (admin.TenantId is null || admin.Tenant?.IsActive != true || principal.FindAll("tenant_id").Count() != 1 ||
                    !Guid.TryParse(principal.FindFirstValue("tenant_id"), out var tenantId) || tenantId != admin.TenantId)) ||
                (admin.Role == AdminRole.IKhetheloAdmin && (admin.TenantId is not null || principal.HasClaim(x => x.Type == "tenant_id")))) context.Fail("Inactive or invalid identity.");
        },
        OnChallenge = context => { context.HandleResponse(); context.Response.Headers.WWWAuthenticate = "Bearer"; return Problems.WriteAsync(context.HttpContext, 401); },
        OnForbidden = context => Problems.WriteAsync(context.HttpContext, 403)
    };
});
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TenantMembership", policy => policy.RequireAuthenticatedUser().AddRequirements(new TenantPolicyRequirement(false)));
    options.AddPolicy("CrossTenantMembership", policy => policy.RequireAuthenticatedUser().AddRequirements(new TenantPolicyRequirement(true)));
    options.AddPolicy("MembershipAccess", policy => policy.RequireAuthenticatedUser().AddRequirements(new TenantPolicyRequirement(null)));
    options.AddPolicy("NtmAccess", policy => policy.RequireAuthenticatedUser().AddRequirements(new NtmAccessRequirement()));
    options.AddPolicy("AdministratorManagement", policy => policy.RequireAuthenticatedUser().AddRequirements(new AdministratorManagementRequirement()));
});
builder.Services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
        options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        options.JsonSerializerOptions.Converters.Add(new StrictEnumConverter<MemberStatus>());
        options.JsonSerializerOptions.Converters.Add(new StrictEnumConverter<MembershipApplicationStatus>());
    });
builder.Services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
{
    var invalidEnum = context.ModelState.Values.SelectMany(x => x.Errors).Any(x => x.ErrorMessage.Contains("validation:status", StringComparison.Ordinal));
    var code = invalidEnum ? 422 : 400;
    var result = new ObjectResult(Problems.Create(context.HttpContext, code, errors: invalidEnum ? new() { ["status"] = ["Choose a declared string status."] } : null)) { StatusCode = code };
    result.ContentTypes.Add("application/problem+json"); return result;
});
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(
    builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []).WithMethods("GET", "POST", "PUT", "PATCH").WithHeaders("Content-Type", "Authorization")));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry)
            ? Math.Max(1, (int)Math.Ceiling(retry.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture) : "60";
        return new ValueTask(Problems.WriteAsync(context.HttpContext, 429));
    };
    var permitLimit = builder.Configuration.GetValue("RateLimits:PerMinute", 5);
    foreach (var policyName in new[] { "PublicSubmission", "Login" })
    {
        options.AddPolicy(policyName, context => RateLimitPartition.GetFixedWindowLimiter(
            (policyName == "PublicSubmission" ? context.Request.RouteValues["tenantCode"] + ":" : "") + context.Connection.RemoteIpAddress,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    }
    // Shared capacity bound for anonymous writes only; protected endpoints retain their documented status codes.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        context.Request.Path.StartsWithSegments("/api/v1/public") || context.Request.Path == "/api/v1/auth/login"
            ? RateLimitPartition.GetFixedWindowLimiter("anonymous", _ => new FixedWindowRateLimiterOptions { PermitLimit = 1000, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
            : RateLimitPartition.GetNoLimiter("other"));
});

var app = builder.Build();
if (adoptDatabase)
{
    if (bootstrap) { Console.Error.WriteLine("Run database adoption and administrator bootstrap separately."); Environment.ExitCode = 1; }
    else Environment.ExitCode = await DatabaseSetupConsole.AdoptAsync(app.Services);
    await app.DisposeAsync(); return;
}
if (bootstrap)
{
    Environment.ExitCode = await BootstrapConsole.RunAsync(app.Services, builder.Configuration["tenant"] ?? "ESHL");
    await app.DisposeAsync(); return;
}
_ = app.Services.GetRequiredService<JwtKeys>();
app.UseForwardedHeaders();
app.UseMiddleware<ErrorMiddleware>();
app.UseStatusCodePages(context => Problems.WriteAsync(context.HttpContext, context.HttpContext.Response.StatusCode));
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    // Only set this when the public edge enforces HTTPS and private HTTP access is controlled.
    if (!builder.Configuration.GetValue<bool>("ReverseProxy:EdgeTerminatesTls")) app.UseHttpsRedirection();
}
app.UseRouting(); app.UseCors();
app.UseAuthentication(); app.UseRateLimiter();
app.UseMiddleware<TenantScopeMiddleware>(); app.UseAuthorization();
app.UseMiddleware<RequestBodyMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.MapGet("/swagger/contract.yaml", () => Results.File(Path.Combine(AppContext.BaseDirectory, "Contract", "ikhethelo-digital-api.openapi.yaml"), "application/yaml")).AllowAnonymous();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/contract.yaml", "iKhethelo Digital API v1"));
}
app.MapHealthChecks("/healthz").AllowAnonymous();
app.MapControllers();
await app.RunAsync();
public partial class Program;
