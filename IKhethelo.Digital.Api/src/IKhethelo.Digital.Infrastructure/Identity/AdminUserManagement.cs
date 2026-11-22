using IKhethelo.Digital.Application;
using IKhethelo.Digital.Domain;
using IKhethelo.Digital.Infrastructure.Audit;
using IKhethelo.Digital.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IKhethelo.Digital.Infrastructure.Identity;

public sealed class AdminUserManagement(DigitalDbContext db, ITenantContext context, AuditWriter audit,
    IPasswordHasher<AdminUser> hasher, TimeProvider clock) : IAdminUserManagement
{
    private TenantAccess Access => context.Current;
    private IQueryable<AdminUser> Users => db.AdminUsers.Where(x => x.TenantId == Access.TenantId && (x.Role == AdminRole.TenantAdmin || x.Role == AdminRole.TenantStaff));
    private Task<int> ActiveTenantAdmins(CancellationToken ct) => Users.CountAsync(x => x.IsActive && x.Role == AdminRole.TenantAdmin, ct);
    private static AdminResponse Response(AdminUser a) => new(a.Id, a.Email, a.Role.ToString(), a.IsActive, a.CreatedAtUtc, a.UpdatedAtUtc, a.LastLoginAtUtc);
    // Serialize tenant account mutations, including competing attempts to deactivate the last administrator.
    private async Task LockTenantAsync(CancellationToken ct)
    {
        var tenant = (await db.Tenants.FromSqlInterpolated($"SELECT * FROM tenants WHERE id = {Access.TenantId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
        if (tenant?.IsActive != true) throw RequestFailure.NotFound();
    }
    private async Task<AdminUser> FindAsync(Guid id, CancellationToken ct) => await Users.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw RequestFailure.NotFound();
    private async Task UniqueEmailAsync(string normalized, Guid? id, CancellationToken ct)
    {
        if (await db.AdminUsers.AnyAsync(x => x.NormalizedEmail == normalized && x.Id != id, ct)) throw new RequestFailure(409, "An administrator already uses this email address.");
    }
    public async Task<AdminResponse> CreateAsync(AdminCreate request, CancellationToken ct)
    {
        var role = request.Role ?? nameof(AdminRole.TenantStaff);
        AdministratorValidation.Email(request.Email); AdministratorValidation.Password(request.Password); AdministratorValidation.Role(role);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockTenantAsync(ct);
        var normalized = Validation.NormalizeEmail(request.Email!); await UniqueEmailAsync(normalized, null, ct);
        var now = clock.GetUtcNow();
        var admin = new AdminUser { Id = Guid.NewGuid(), TenantId = Access.TenantId, Email = request.Email!.Trim(), NormalizedEmail = normalized,
            Role = Enum.Parse<AdminRole>(role), IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now };
        admin.PasswordHash = hasher.HashPassword(admin, request.Password!); db.AdminUsers.Add(admin);
        audit.Add("CreateAdministrator", "AdminUser", admin.Id);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Response(admin);
    }
    public async Task<Page<AdminResponse>> ListAsync(AdminListQuery query, CancellationToken ct)
    {
        Validation.Query<MemberStatus>(new(query.Page, query.PageSize, query.Search));
        var users = Users.AsNoTracking();
        if (query.IsActive is { } active) users = users.Where(x => x.IsActive == active);
        if (query.Search?.Trim() is { Length: > 0 } search) { var normalized = Validation.NormalizeEmail(search); users = users.Where(x => x.NormalizedEmail.Contains(normalized)); }
        var count = await users.LongCountAsync(ct); var offset = ((long)query.Page - 1) * query.PageSize;
        var rows = offset >= count ? [] : await users.OrderBy(x => x.NormalizedEmail).ThenBy(x => x.Id).Skip((int)offset).Take(query.PageSize).ToListAsync(ct);
        await audit.ReadAsync("AdminUser", null, ct);
        return new(rows.Select(Response).ToArray(), query.Page, query.PageSize, count);
    }
    public async Task<AdminResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var admin = await FindAsync(id, ct); await audit.ReadAsync("AdminUser", id, ct); return Response(admin);
    }
    public async Task<AdminResponse> UpdateAsync(Guid id, AdminUpdate request, CancellationToken ct)
    {
        AdministratorValidation.Email(request.Email); AdministratorValidation.Role(request.Role);
        await using var transaction = await db.Database.BeginTransactionAsync(ct); await LockTenantAsync(ct);
        var admin = await FindAsync(id, ct); var normalized = Validation.NormalizeEmail(request.Email!);
        var newRole = Enum.Parse<AdminRole>(request.Role!);
        if (admin.IsActive && admin.Role == AdminRole.TenantAdmin && newRole != AdminRole.TenantAdmin && await ActiveTenantAdmins(ct) <= 1)
            throw new RequestFailure(409, "You cannot deactivate or demote the last active Organisation Administrator.");
        var oldRole = admin.Role;
        await UniqueEmailAsync(normalized, id, ct);
        var email = request.Email!.Trim(); var now = clock.GetUtcNow();
        // The existing schema's normalized-email UNIQUE constraint is mapped as an EF alternate
        // key. Update it in SQL rather than mutating an immutable tracked key; preserve the constraint.
        await Users.Where(x => x.Id == id).ExecuteUpdateAsync(set => set
            .SetProperty(x => x.Email, email).SetProperty(x => x.NormalizedEmail, normalized)
            .SetProperty(x => x.Role, newRole)
            .SetProperty(x => x.UpdatedAtUtc, now).SetProperty(x => x.SecurityVersion, x => x.SecurityVersion + 1), ct);
        db.Entry(admin).State = EntityState.Detached;
        audit.Add("UpdateAdministrator", "AdminUser", id, new { previousRole = oldRole.ToString(), role = newRole.ToString() });
        await db.SaveChangesAsync(ct);
        var updated = await Users.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        await transaction.CommitAsync(ct); return Response(updated);
    }
    public async Task<AdminResponse> StatusAsync(Guid id, AdminStatusUpdate request, CancellationToken ct)
    {
        if (request.IsActive is null) throw new RequestFailure(422, "isActive is required.", new() { ["isActive"] = ["Choose true or false."] });
        await using var transaction = await db.Database.BeginTransactionAsync(ct); await LockTenantAsync(ct);
        var admin = await FindAsync(id, ct);
        if (!request.IsActive.Value && id == Access.ActorId)
            throw new RequestFailure(409, "You cannot deactivate your own account.");
        if (!request.IsActive.Value && admin.IsActive && admin.Role == AdminRole.TenantAdmin && await ActiveTenantAdmins(ct) <= 1)
            throw new RequestFailure(409, "You cannot deactivate or demote the last active Organisation Administrator.");
        var previous = admin.IsActive;
        if (previous != request.IsActive.Value) { admin.IsActive = request.IsActive.Value; admin.SecurityVersion++; admin.UpdatedAtUtc = clock.GetUtcNow(); }
        audit.Add("SetAdministratorStatus", "AdminUser", id, new { previousIsActive = previous, isActive = admin.IsActive });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Response(admin);
    }
    public async Task ResetPasswordAsync(Guid id, AdminPasswordReset request, CancellationToken ct)
    {
        if (id == Access.ActorId) throw new RequestFailure(403, "Use change password for your own account.");
        AdministratorValidation.Password(request.NewPassword);
        await using var transaction = await db.Database.BeginTransactionAsync(ct); await LockTenantAsync(ct);
        var admin = (await db.AdminUsers.FromSqlInterpolated($"SELECT * FROM admin_users WHERE id = {id} AND tenant_id = {Access.TenantId} AND role IN ('TenantAdmin', 'TenantStaff') FOR UPDATE").ToListAsync(ct)).SingleOrDefault() ?? throw RequestFailure.NotFound();
        admin.PasswordHash = hasher.HashPassword(admin, request.NewPassword!); admin.SecurityVersion++; admin.UpdatedAtUtc = clock.GetUtcNow();
        audit.Add("ResetAdministratorPassword", "AdminUser", id);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
}

public sealed class ChangeOwnPasswordService(DigitalDbContext db, IPasswordHasher<AdminUser> hasher, TimeProvider clock) : IChangeOwnPassword
{
    public async Task ExecuteAsync(Guid actorId, int securityVersion, ChangeOwnPassword request, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(request.CurrentPassword) || request.CurrentPassword.Length > 1024)
            throw new RequestFailure(422, "Enter your current password.", new() { ["currentPassword"] = ["Enter your current password."] });
        AdministratorValidation.Password(request.NewPassword);
        if (request.NewPassword != request.ConfirmPassword)
            throw new RequestFailure(422, "New password and confirmation do not match.", new() { ["confirmPassword"] = ["Passwords do not match."] });
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var admin = (await db.AdminUsers.FromSqlInterpolated($"SELECT * FROM admin_users WHERE id = {actorId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
        if (admin is null || !admin.IsActive || admin.SecurityVersion != securityVersion)
            throw new RequestFailure(401, "Authentication is required.");
        if (hasher.VerifyHashedPassword(admin, admin.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            throw new RequestFailure(422, "Current password is incorrect.", new() { ["currentPassword"] = ["Current password is incorrect."] });
        if (hasher.VerifyHashedPassword(admin, admin.PasswordHash, request.NewPassword!) != PasswordVerificationResult.Failed)
            throw new RequestFailure(422, "Choose a password different from your current password.", new() { ["password"] = ["Choose a password different from your current password."] });
        admin.PasswordHash = hasher.HashPassword(admin, request.NewPassword!);
        admin.SecurityVersion++; admin.UpdatedAtUtc = clock.GetUtcNow();
        db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), TenantId = admin.TenantId, ActorAdminUserId = admin.Id,
            Action = "ChangeOwnPassword", EntityType = "AdminUser", EntityId = admin.Id, CreatedAtUtc = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
}

public sealed class BootstrapAdministrator(DigitalDbContext db, IPasswordHasher<AdminUser> hasher, TimeProvider clock)
{
    public async Task<Guid> ExecuteAsync(string tenantCode, string email, string password, CancellationToken ct)
    {
        AdministratorValidation.Email(email); AdministratorValidation.Password(password);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var tenant = (await db.Tenants.FromSqlInterpolated($"SELECT * FROM tenants WHERE code = {tenantCode} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
        if (tenant?.IsActive != true) throw RequestFailure.NotFound();
        // This is initial setup, never an account recovery or reactivation backdoor.
        if (await db.AdminUsers.AnyAsync(x => x.TenantId == tenant.Id, ct)) throw RequestFailure.Conflict();
        var normalized = Validation.NormalizeEmail(email);
        if (await db.AdminUsers.AnyAsync(x => x.NormalizedEmail == normalized, ct)) throw RequestFailure.Conflict();
        var now = clock.GetUtcNow();
        var admin = new AdminUser { Id = Guid.NewGuid(), TenantId = tenant.Id, Email = email.Trim(), NormalizedEmail = normalized,
            Role = AdminRole.TenantAdmin, IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now };
        admin.PasswordHash = hasher.HashPassword(admin, password); db.AdminUsers.Add(admin);
        db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), TenantId = tenant.Id, ActorAdminUserId = admin.Id,
            Action = "BootstrapAdministrator", EntityType = "AdminUser", EntityId = admin.Id, CreatedAtUtc = now,
            CorrelationId = Guid.NewGuid().ToString(), MetadataJson = "{\"provisioningMethod\":\"trusted-console\"}" });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return admin.Id;
    }
}

// Trusted-console recovery only: callers must verify the database and operator intent.
public sealed class RecoverTenantAdministrator(DigitalDbContext db, IPasswordHasher<AdminUser> hasher, TimeProvider clock)
{
    public async Task<Guid> ExecuteAsync(string tenantCode, string email, string password, CancellationToken ct)
    {
        AdministratorValidation.Email(email); AdministratorValidation.Password(password);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var tenant = (await db.Tenants.FromSqlInterpolated($"SELECT * FROM tenants WHERE code = {tenantCode} FOR UPDATE").ToListAsync(ct)).SingleOrDefault();
        if (tenant?.IsActive != true) throw RequestFailure.NotFound();
        var normalized = Validation.NormalizeEmail(email);
        if (await db.AdminUsers.AnyAsync(x => x.NormalizedEmail == normalized, ct)) throw RequestFailure.Conflict();
        var now = clock.GetUtcNow();
        var admin = new AdminUser { Id = Guid.NewGuid(), TenantId = tenant.Id, Email = email.Trim(), NormalizedEmail = normalized,
            Role = AdminRole.TenantAdmin, IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now };
        admin.PasswordHash = hasher.HashPassword(admin, password);
        db.AdminUsers.Add(admin);
        db.AuditLogs.Add(new AuditLog { Id = Guid.NewGuid(), TenantId = tenant.Id, ActorAdminUserId = admin.Id,
            Action = "RecoverTenantAdministrator", EntityType = "AdminUser", EntityId = admin.Id, CreatedAtUtc = now,
            CorrelationId = Guid.NewGuid().ToString(), MetadataJson = "{\"provisioningMethod\":\"trusted-console-recovery\"}" });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return admin.Id;
    }
}
