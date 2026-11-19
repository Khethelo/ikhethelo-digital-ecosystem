using IKhethelo.Digital.Domain;

namespace IKhethelo.Digital.Application;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken);
}

public sealed record ApplicationSubmission(string? FirstName, string? LastName, DateOnly? DateOfBirth,
    string? PhoneNumber, string? Email, bool? TermsAccepted, bool? PrivacyConsent);
public sealed record MemberUpdate(string? FirstName, string? LastName, string? PhoneNumber, string? Email);
public sealed record MemberStatusUpdate(MemberStatus? Status);
public sealed record ReviewRequest(string? ReviewNotes);
public sealed record LoginRequest(string? Email, string? Password);
public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);
public sealed record SubmissionReceipt(Guid ApplicationId, MembershipApplicationStatus Status, DateTimeOffset SubmittedAtUtc, string Message);
public sealed record ApplicationResponse(Guid ApplicationId, string FirstName, string LastName, DateOnly DateOfBirth,
    string PhoneNumber, string Email, bool TermsAccepted, bool PrivacyConsent, MembershipApplicationStatus Status, DateTimeOffset SubmittedAtUtc);
public sealed record MemberResponse(Guid MemberId, Guid ApplicationId, string MemberNumber, string FirstName, string LastName,
    DateOnly DateOfBirth, string PhoneNumber, string Email, MemberStatus Status, DateTimeOffset JoinedAtUtc);
public sealed record Page<T>(IReadOnlyList<T> Items, [property: System.Text.Json.Serialization.JsonPropertyName("page")] int PageNumber, int PageSize, long TotalCount);
public sealed record ListQuery(int Page = 1, int PageSize = 20, string? Search = null, string? Status = null);

// Established only after server tenant resolution and policy authorization.
public sealed record TenantAccess(Guid TenantId, string TenantCode, Guid? ActorId, bool IsInternal, string CorrelationId);
public interface ITenantContext { TenantAccess Current { get; } }
public interface ISubmitMembershipApplication
{
    Task<SubmissionReceipt> ExecuteAsync(ApplicationSubmission request, CancellationToken ct);
}
public interface IMembershipQueries
{
    Task<Page<ApplicationResponse>> ApplicationsAsync(ListQuery query, CancellationToken ct);
    Task<ApplicationResponse> ApplicationAsync(Guid id, CancellationToken ct);
    Task<Page<MemberResponse>> MembersAsync(ListQuery query, CancellationToken ct);
    Task<MemberResponse> MemberAsync(Guid id, CancellationToken ct);
}
public interface IReviewApplications
{
    Task<MemberResponse> ActivateAsync(Guid id, ReviewRequest request, CancellationToken ct);
}
public interface IMemberAdministration
{
    Task<MemberResponse> UpdateAsync(Guid id, MemberUpdate request, CancellationToken ct);
    Task<MemberResponse> StatusAsync(Guid id, MemberStatusUpdate request, CancellationToken ct);
}
public interface IAdministratorAuthentication { Task<TokenResponse?> ExecuteAsync(LoginRequest request, CancellationToken ct); }
public interface ITokenIssuer { TokenResponse Issue(AdminUser admin); }

public sealed class RequestFailure(int status, string message, Dictionary<string, string[]>? errors = null) : Exception(message)
{
    public int Status { get; } = status;
    public Dictionary<string, string[]>? Errors { get; } = errors;
    public static RequestFailure NotFound() => new(404, "The requested resource was not found.");
    public static RequestFailure Conflict() => new(409, "The request conflicts with existing data or current state.");
}

public static class Projection
{
    public static ApplicationResponse Response(this MembershipApplication a) => new(a.Id, a.FirstName, a.LastName, a.DateOfBirth,
        a.PhoneNumber, a.Email, a.TermsAccepted, a.PrivacyConsent, a.Status, a.SubmittedAtUtc);
    public static MemberResponse Response(this Member m) => new(m.Id, m.ApplicationId, m.MemberNumber, m.FirstName, m.LastName,
        m.DateOfBirth, m.PhoneNumber, m.Email, m.Status, m.JoinedAtUtc);
}
