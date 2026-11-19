namespace IKhethelo.Digital.Domain;

// Workbook mapping and source-quality exceptions: docs/NTM_SPREADSHEET_FIELD_MAPPING.md.
public sealed class NtmVeteran
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? MkmvaNumber { get; set; }
    public string? DmvNumber { get; set; }
    public string? ContactNumber { get; set; }
    public string? EmailAddress { get; set; }
    public string? MembershipReference { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public NtmBusiness? Business { get; set; }
}

public sealed class NtmBusiness
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid VeteranId { get; set; }
    public NtmVeteran Veteran { get; set; } = null!;
    public string BusinessName { get; set; } = null!;
    public string? CipcRegistrationNumber { get; set; }
    public string? BusinessAddress { get; set; }
    public string? Sector { get; set; }
    public string? BusinessBackground { get; set; }
    public string? OperationalStatus { get; set; }
    public int? NumberOfEmployees { get; set; }
    public decimal? YearsInOperation { get; set; }
    public string? CurrentBusinessNeeds { get; set; }
    public string? PriorityNeed { get; set; }
    public bool? FundingRequired { get; set; }
    public decimal? FundingAmount { get; set; }
    public string? AdditionalComments { get; set; }
    public DateOnly? AssessmentDate { get; set; }
    public string? AssessedBy { get; set; }
    public int? SourceRecordNumber { get; set; }
    public string? OperationalStatusSource { get; set; }
    public string? NumberOfEmployeesSource { get; set; }
    public string? YearsInOperationSource { get; set; }
    public string? FundingRequiredSource { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
