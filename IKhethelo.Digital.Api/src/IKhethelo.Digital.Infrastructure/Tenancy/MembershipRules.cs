namespace IKhethelo.Digital.Infrastructure.Tenancy;

public sealed class MembershipRules
{
    public Dictionary<string, TenantMembershipRules> Tenants { get; set; } = new();
    public TenantMembershipRules For(string code) => Tenants.GetValueOrDefault(code) ?? new();
}

public sealed class TenantMembershipRules
{
    public bool RequireAdult { get; set; }
    public string? MemberNumberPrefix { get; set; }
}
