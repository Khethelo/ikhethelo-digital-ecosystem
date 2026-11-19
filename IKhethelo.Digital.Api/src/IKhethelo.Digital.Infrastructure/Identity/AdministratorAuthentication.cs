using IKhethelo.Digital.Application;
using IKhethelo.Digital.Domain;
using IKhethelo.Digital.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IKhethelo.Digital.Infrastructure.Identity;

public sealed class PasswordTimingGuard(IPasswordHasher<AdminUser> hasher)
{
    public string DummyHash { get; } = hasher.HashPassword(new AdminUser(), Guid.NewGuid().ToString());
}

public sealed class AdministratorAuthentication(DigitalDbContext db, IPasswordHasher<AdminUser> hasher,
    PasswordTimingGuard timing, ITokenIssuer tokens, TimeProvider clock) : IAdministratorAuthentication
{
    public async Task<TokenResponse?> ExecuteAsync(LoginRequest request, CancellationToken ct)
    {
        Validation.Login(request);
        var normalized = Validation.NormalizeEmail(request.Email!);
        var admin = await db.AdminUsers.Include(x => x.Tenant).SingleOrDefaultAsync(x => x.NormalizedEmail == normalized, ct); // It crashes on this line
        var verified = hasher.VerifyHashedPassword(admin ?? new AdminUser(), admin?.PasswordHash ?? timing.DummyHash, request.Password!);
        if (admin is null || verified == PasswordVerificationResult.Failed || !admin.IsActive ||
            (admin.Role == AdminRole.TenantAdmin && (admin.TenantId is null || admin.Tenant?.IsActive != true)) ||
            (admin.Role == AdminRole.IKhetheloAdmin && admin.TenantId is not null) || !Enum.IsDefined(admin.Role))
            return null;
        // Token is issued only after successful required persistence; never log it.
        var now = clock.GetUtcNow();
        if (verified == PasswordVerificationResult.SuccessRehashNeeded) admin.PasswordHash = hasher.HashPassword(admin, request.Password!);
        admin.LastLoginAtUtc = now; admin.UpdatedAtUtc = now;
        db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), TenantId = admin.TenantId, ActorAdminUserId = admin.Id,
            Action = "AdministratorLogin", EntityType = "AdminUser", EntityId = admin.Id, CreatedAtUtc = now });
        await db.SaveChangesAsync(ct);
        return tokens.Issue(admin);
    }
}
