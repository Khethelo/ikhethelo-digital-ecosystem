using IKhethelo.Digital.Application;
using IKhethelo.Digital.Domain;
using IKhethelo.Digital.Infrastructure.Persistence;

namespace IKhethelo.Digital.Infrastructure.Tenancy;

public sealed class ResolvedTenantContext : ITenantContext
{
    private TenantAccess? access;
    public TenantAccess Current => access ?? throw new InvalidOperationException("Tenant context has not been resolved.");
    public void Set(TenantAccess value)
    {
        if (access is not null) throw new InvalidOperationException("Tenant context is immutable.");
        access = value;
    }
}

// All membership reads begin here. There is no controller-facing unscoped repository.
public sealed class TenantData(DigitalDbContext db, ITenantContext tenant)
{
    public TenantAccess Access => tenant.Current;
    public IQueryable<MembershipApplication> Applications => db.Applications.Where(x => x.TenantId == Access.TenantId);
    public IQueryable<Member> Members => db.Members.Where(x => x.TenantId == Access.TenantId);
}
