namespace IKhethelo.Digital.Application;

public sealed record NtmVeteranWrite(string? FullName, string? MkmvaNumber = null, string? DmvNumber = null, string? ContactNumber = null,
    string? EmailAddress = null, string? MembershipReference = null);
public sealed record NtmBusinessWrite(Guid VeteranId, string? BusinessName, string? CipcRegistrationNumber = null,
    string? BusinessAddress = null, string? Sector = null, string? BusinessBackground = null,
    string? OperationalStatus = null, int? NumberOfEmployees = null, decimal? YearsInOperation = null,
    string? CurrentBusinessNeeds = null, string? PriorityNeed = null, bool? FundingRequired = null,
    decimal? FundingAmount = null, string? AdditionalComments = null, DateOnly? AssessmentDate = null, string? AssessedBy = null);
public sealed record NtmBusinessSummary(Guid Id, string BusinessName);
public sealed record NtmVeteranResponse(Guid Id, string FullName, string? MkmvaNumber, string? DmvNumber,
    string? ContactNumber, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, NtmBusinessSummary? Business,
    string? EmailAddress = null, string? MembershipReference = null);
public sealed record NtmBusinessResponse(Guid Id, Guid VeteranId, string BusinessName, string? CipcRegistrationNumber,
    string? BusinessAddress, string? Sector, string? BusinessBackground, string? OperationalStatus,
    int? NumberOfEmployees, decimal? YearsInOperation, string? CurrentBusinessNeeds, string? PriorityNeed,
    bool? FundingRequired, decimal? FundingAmount, string? AdditionalComments, DateOnly? AssessmentDate,
    string? AssessedBy, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
public sealed record NtmListQuery(int Page = 1, int PageSize = 20, string? Search = null, string? Sector = null,
    string? OperationalStatus = null, Guid? VeteranId = null, bool? HasBusiness = null);
public sealed record NtmCount(string Value, int Count);
public sealed record NtmDashboard(int TotalVeterans, int TotalBusinesses, int OperationalBusinesses,
    int PartiallyOperationalBusinesses, int NotOperationalBusinesses, decimal TotalRecordedFundingAmount,
    int FundingRequiredBusinesses, IReadOnlyList<NtmCount> BusinessesBySector, IReadOnlyList<NtmCount> RecordedPriorityNeeds);
public interface INtmOperations
{
    Task<Page<NtmVeteranResponse>> VeteransAsync(NtmListQuery query, CancellationToken ct);
    Task<NtmVeteranResponse> VeteranAsync(Guid id, CancellationToken ct);
    Task<NtmVeteranResponse> CreateVeteranAsync(NtmVeteranWrite request, CancellationToken ct);
    Task<NtmVeteranResponse> UpdateVeteranAsync(Guid id, NtmVeteranWrite request, CancellationToken ct);
    Task<Page<NtmBusinessResponse>> BusinessesAsync(NtmListQuery query, CancellationToken ct);
    Task<NtmBusinessResponse> BusinessAsync(Guid id, CancellationToken ct);
    Task<NtmBusinessResponse> CreateBusinessAsync(NtmBusinessWrite request, CancellationToken ct);
    Task<NtmBusinessResponse> UpdateBusinessAsync(Guid id, NtmBusinessWrite request, CancellationToken ct);
    Task<NtmDashboard> DashboardAsync(CancellationToken ct);
}

public static class NtmValidation
{
    // Confirmed by the workbook Instructions sheet. Source anomalies are preserved separately.
    public static readonly IReadOnlyList<string> OperationalStatuses = Array.AsReadOnly(new[] { "Operational", "Partially Operational", "Not Operational" });
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void Text(Dictionary<string, string[]> errors, string key, string? value, int max, bool required = false)
    {
        if ((required && Clean(value) is null) || (Clean(value)?.EnumerateRunes().Count() ?? 0) > max)
            errors[key] = [$"Provide {(required ? "a nonblank value of " : "at most ")}{max} characters or fewer."];
    }
    private static void Finish(Dictionary<string, string[]> errors)
    {
        if (errors.Count != 0) throw new RequestFailure(422, "Validation failed.", errors);
    }
    public static void Veteran(NtmVeteranWrite request)
    {
        var errors = new Dictionary<string, string[]>();
        Text(errors, "fullName", request.FullName, 200, true);
        Text(errors, "mkmvaNumber", request.MkmvaNumber, 100); Text(errors, "dmvNumber", request.DmvNumber, 100);
        Text(errors, "contactNumber", request.ContactNumber, 50);
        // Recorded source contact information; no outbound email or account creation uses this field.
        Text(errors, "emailAddress", request.EmailAddress, 320);
        if ((request.MembershipReference?.EnumerateRunes().Count() ?? 0) > 100)
            errors["membershipReference"] = ["Use at most 100 characters; original source text is retained verbatim."];
        if (Clean(request.MkmvaNumber) is null && Clean(request.DmvNumber) is null)
            errors["registration"] = ["Provide at least one MKMVA or DMV registration number."];
        Finish(errors);
    }
    public static void Business(NtmBusinessWrite request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.VeteranId == Guid.Empty) errors["veteranId"] = ["Select a veteran."];
        Text(errors, "businessName", request.BusinessName, 200, true);
        Text(errors, "cipcRegistrationNumber", request.CipcRegistrationNumber, 100);
        Text(errors, "sector", request.Sector, 200); Text(errors, "assessedBy", request.AssessedBy, 200);
        if (Clean(request.OperationalStatus) is { } status && !OperationalStatuses.Contains(status, StringComparer.Ordinal))
            errors["operationalStatus"] = ["Choose a declared operational status."];
        if (request.NumberOfEmployees < 0) errors["numberOfEmployees"] = ["Must be nonnegative."];
        if (request.YearsInOperation < 0) errors["yearsInOperation"] = ["Must be nonnegative."];
        if (request.FundingAmount < 0) errors["fundingAmount"] = ["Must be nonnegative."];
        if (request.YearsInOperation is { } years && (years > 99999.9m || decimal.Round(years, 1) != years))
            errors["yearsInOperation"] = ["Use at most five integer digits and one decimal place."];
        if (request.FundingAmount is { } amount && (amount > 9999999999999.99m || decimal.Round(amount, 2) != amount))
            errors["fundingAmount"] = ["Use at most thirteen integer digits and two decimal places (Rand)."];
        Finish(errors);
    }
    public static void Query(NtmListQuery query)
    {
        var errors = new Dictionary<string, string[]>();
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)(query.Page - 1) * query.PageSize > int.MaxValue)
            errors["page"] = ["Use page >= 1, pageSize 1–100 and a supported offset."];
        Text(errors, "search", query.Search, 200); Text(errors, "sector", query.Sector, 200);
        if (Clean(query.OperationalStatus) is { } status && !OperationalStatuses.Contains(status, StringComparer.Ordinal))
            errors["operationalStatus"] = ["Choose a declared operational status."];
        Finish(errors);
    }
}
