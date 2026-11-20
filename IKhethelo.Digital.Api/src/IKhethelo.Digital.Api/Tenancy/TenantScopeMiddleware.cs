using System.Security.Claims;
using System.Text.RegularExpressions;
using IKhethelo.Digital.Application;
using IKhethelo.Digital.Domain;
using IKhethelo.Digital.Infrastructure.Persistence;
using IKhethelo.Digital.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace IKhethelo.Digital.Api.Tenancy;

public sealed class TenantRouting
{
    public Dictionary<string, string> Aliases { get; set; } = new(StringComparer.Ordinal) { ["eshlahleni"] = "ESHL" };
    // Trusted configuration grants specific administrators access to specific tenant codes.
    public Dictionary<string, string[]> CrossTenantMembership { get; set; } = new();
    public Dictionary<string, string[]> AdminUserManagement { get; set; } = new();
    public Dictionary<string, string[]> CrossTenantNtm { get; set; } = new();
    // Explicit opt-in prevents exposing a provisional schema in existing installations.
    public string[] NtmTenantCodes { get; set; } = [];
}

public sealed class TenantScopeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, DigitalDbContext db, ResolvedTenantContext tenantContext, TenantRouting routing)
    {
        var alias = context.Request.RouteValues["tenantCode"] as string;
        if (context.Request.Path.StartsWithSegments("/api/v1/admin-users"))
        {
            if (context.User.Identity?.IsAuthenticated != true) { await next(context); return; }
            if (context.Request.Query["tenantCode"].Count > 1) throw new RequestFailure(400, "Select exactly one tenant.");
            alias = context.Request.Query["tenantCode"].SingleOrDefault();
            if (string.IsNullOrEmpty(alias))
            {
                if (!Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var assigned))
                    throw new RequestFailure(400, "Internal administrators must select tenantCode.");
                alias = await db.Tenants.Where(x => x.Id == assigned && x.IsActive).Select(x => x.Code).SingleOrDefaultAsync(context.RequestAborted)
                    ?? throw RequestFailure.NotFound();
            }
        }
        if (alias is null) { await next(context); return; }
        var administrative = context.Request.Path.StartsWithSegments("/api/v1/admin") || context.Request.Path.StartsWithSegments("/api/v1/admin-users");
        if (administrative && context.User.Identity?.IsAuthenticated != true) { await next(context); return; }
        if (!Regex.IsMatch(alias, "^[A-Za-z][A-Za-z0-9-]{0,49}$")) throw RequestFailure.NotFound();
        if (!administrative && !routing.Aliases.ContainsKey(alias)) throw RequestFailure.NotFound();
        var code = routing.Aliases.TryGetValue(alias, out var mapped) ? mapped : alias;
        var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Code == code && x.IsActive, context.RequestAborted) ?? throw RequestFailure.NotFound();
        Guid? actor = null; var isInternal = false;
        if (administrative)
        {
            if (!Guid.TryParse(context.User.FindFirstValue("sub"), out var actorId)) throw new RequestFailure(401, "Authentication is required.");
            actor = actorId; isInternal = context.User.IsInRole(nameof(AdminRole.IKhetheloAdmin));
            if (!isInternal && (!(context.User.IsInRole(nameof(AdminRole.TenantAdmin)) || context.User.IsInRole(nameof(AdminRole.TenantStaff))) ||
                !Guid.TryParse(context.User.FindFirstValue("tenant_id"), out var claimTenant) || claimTenant != tenant.Id))
                throw new RequestFailure(403, "Access is forbidden.");
        }
        tenantContext.Set(new(tenant.Id, tenant.Code, actor, isInternal, context.TraceIdentifier));
        context.Items["ResolvedTenantCode"] = tenant.Code;
        await next(context);
    }
}

public sealed record AdministratorManagementRequirement : IAuthorizationRequirement;
public sealed record NtmAccessRequirement : IAuthorizationRequirement;
public sealed class NtmAccessHandler(ITenantContext tenant, TenantRouting routing) : AuthorizationHandler<NtmAccessRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, NtmAccessRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var access = tenant.Current;
            if (routing.NtmTenantCodes.Contains(access.TenantCode, StringComparer.Ordinal) && access.ActorId is { } actor &&
                ((!access.IsInternal && (context.User.IsInRole("TenantAdmin") || context.User.IsInRole("TenantStaff"))) ||
                (access.IsInternal && context.User.IsInRole("IKhetheloAdmin") &&
                routing.CrossTenantNtm.TryGetValue(actor.ToString(), out var codes) && codes.Contains(access.TenantCode, StringComparer.Ordinal))))
                context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
public sealed class AdministratorManagementHandler(ITenantContext tenant, TenantRouting routing) : AuthorizationHandler<AdministratorManagementRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdministratorManagementRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var access = tenant.Current;
            if (access.ActorId is { } actor && ((!access.IsInternal && context.User.IsInRole("TenantAdmin")) ||
                (access.IsInternal && context.User.IsInRole("IKhetheloAdmin") && routing.AdminUserManagement.TryGetValue(actor.ToString(), out var codes) &&
                 codes.Contains(access.TenantCode, StringComparer.Ordinal)))) context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

public sealed record TenantPolicyRequirement(bool? InternalOnly) : IAuthorizationRequirement;
public sealed class TenantPolicyHandler(ITenantContext tenant, TenantRouting routing, IServiceProvider services) : AuthorizationHandler<TenantPolicyRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantPolicyRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;
        var access = tenant.Current;
        var allowed = access.ActorId is not null && (requirement.InternalOnly is null || access.IsInternal == requirement.InternalOnly);
        if (allowed && access.IsInternal)
        {
            allowed = requirement.InternalOnly is null
                ? (await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(context.User, null, "CrossTenantMembership")).Succeeded
                : routing.CrossTenantMembership.TryGetValue(access.ActorId!.Value.ToString(), out var codes) &&
                  codes.Contains(access.TenantCode, StringComparer.Ordinal);
        }
        if (allowed && (context.User.IsInRole(access.IsInternal ? "IKhetheloAdmin" : "TenantAdmin") ||
            (!access.IsInternal && access.TenantCode == "ESHL" && requirement.InternalOnly is null && context.User.IsInRole("TenantStaff")))) context.Succeed(requirement);
    }
}
