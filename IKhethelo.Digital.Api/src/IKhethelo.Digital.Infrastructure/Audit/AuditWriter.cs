using IKhethelo.Digital.Application;
using IKhethelo.Digital.Domain;
using IKhethelo.Digital.Infrastructure.Persistence;

namespace IKhethelo.Digital.Infrastructure.Audit;

public sealed class AuditWriter(DigitalDbContext db, ITenantContext tenant, TimeProvider clock)
{
    public void Add(string action, string entityType, Guid? entityId, object? changes = null)
    {
        var access = tenant.Current;
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(), TenantId = access.TenantId, ActorAdminUserId = access.ActorId,
            Action = action, EntityType = entityType, EntityId = entityId, CorrelationId = access.CorrelationId,
            CreatedAtUtc = clock.GetUtcNow(), MetadataJson = changes is null
                ? (access.IsInternal ? "{\"crossTenant\":true}" : null)
                : System.Text.Json.JsonSerializer.Serialize(new { crossTenant = access.IsInternal, changes })
        });
    }
    public async Task ReadAsync(string entityType, Guid? id, CancellationToken ct)
    {
        if (!tenant.Current.IsInternal) return;
        Add("CrossTenantRead", entityType, id);
        await db.SaveChangesAsync(ct);
    }
}
