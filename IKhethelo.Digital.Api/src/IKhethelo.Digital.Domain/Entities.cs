namespace IKhethelo.Digital.Domain;

public enum MembershipApplicationStatus { AwaitingInPersonCompletion, Completed, LegacyClosed }
public enum MemberStatus { Active, Suspended, Inactive }
public enum AdminRole { TenantAdmin = 0, IKhetheloAdmin = 1, TenantStaff = 2 }

public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class AdminUser
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public string Email { get; set; } = null!;
    public string NormalizedEmail { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public AdminRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? LastLoginAtUtc { get; set; }
    public int SecurityVersion { get; set; }
}

public sealed class MembershipApplication
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public DateOnly DateOfBirth { get; set; }
    public string PhoneNumber { get; set; } = null!;
    public string NormalizedPhoneNumber { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string NormalizedEmail { get; set; } = null!;
    public MembershipApplicationStatus Status { get; set; }
    public bool TermsAccepted { get; set; }
    public bool PrivacyConsent { get; set; }
    public DateTimeOffset SubmittedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public Guid? ReviewedByAdminId { get; set; }
    public AdminUser? ReviewedByAdmin { get; set; }
    public string? ReviewNotes { get; set; }
}

public sealed class Member
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid ApplicationId { get; set; }
    public MembershipApplication Application { get; set; } = null!;
    public Guid? CategoryId { get; set; }
    public MembershipCategory? Category { get; set; }
    public string MemberNumber { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public DateOnly DateOfBirth { get; set; }
    public string PhoneNumber { get; set; } = null!;
    public string NormalizedPhoneNumber { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string NormalizedEmail { get; set; } = null!;
    public MemberStatus Status { get; set; }
    public DateTimeOffset JoinedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class MembershipCategory
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = null!;
    public string NormalizedName { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class AuditLog
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid? ActorAdminUserId { get; set; }
    public AdminUser? ActorAdminUser { get; set; }
    public string Action { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public Guid? EntityId { get; set; }
    public string? CorrelationId { get; set; }
    public string? MetadataJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
