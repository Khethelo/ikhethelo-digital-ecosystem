using IKhethelo.Digital.Domain;

namespace IKhethelo.Digital.Application;

public sealed record AdminCreate(string? Email, string? Password, string? Role);
public sealed record AdminUpdate(string? Email, string? Role);
public sealed record AdminStatusUpdate(bool? IsActive);
public sealed record AdminPasswordReset(string? NewPassword);
public sealed record ChangeOwnPassword(string? CurrentPassword, string? NewPassword, string? ConfirmPassword);
public interface IChangeOwnPassword { Task ExecuteAsync(Guid actorId, int securityVersion, ChangeOwnPassword request, CancellationToken ct); }
public sealed record AdminResponse(Guid Id, string Email, string Role, bool IsActive,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateTimeOffset? LastLoginAtUtc);
public sealed record AdminListQuery(int Page = 1, int PageSize = 20, string? Search = null, bool? IsActive = null);
public interface IAdminUserManagement
{
    Task<AdminResponse> CreateAsync(AdminCreate request, CancellationToken ct);
    Task<Page<AdminResponse>> ListAsync(AdminListQuery query, CancellationToken ct);
    Task<AdminResponse> GetAsync(Guid id, CancellationToken ct);
    Task<AdminResponse> UpdateAsync(Guid id, AdminUpdate request, CancellationToken ct);
    Task<AdminResponse> StatusAsync(Guid id, AdminStatusUpdate request, CancellationToken ct);
    Task ResetPasswordAsync(Guid id, AdminPasswordReset request, CancellationToken ct);
}

public sealed record MemberRegistration(string? FirstName, string? LastName, DateOnly? DateOfBirth,
    string? PhoneNumber, string? Email, bool? TermsAccepted, bool? PrivacyConsent, Guid? CategoryId = null);
public sealed record CategoryWrite(string? Name, bool? IsActive);
public sealed record CategoryResponse(Guid Id, string Name, bool IsActive);
public sealed record MemberCategoryUpdate(Guid? CategoryId);
public sealed record MemberCategoryResponse(Guid MemberId, Guid? CategoryId, string? CategoryName);
public sealed record MemberHistoryEntry(Guid Id, string Action, Guid? ActorAdminUserId, DateTimeOffset CreatedAtUtc,
    string? PreviousStatus, string? Status, Guid? PreviousCategoryId, Guid? CategoryId);
public interface IMemberOperations
{
    Task<MemberResponse> RegisterAsync(MemberRegistration request, CancellationToken ct);
    Task<Page<CategoryResponse>> CategoriesAsync(ListQuery query, CancellationToken ct);
    Task<CategoryResponse> CreateCategoryAsync(CategoryWrite request, CancellationToken ct);
    Task<CategoryResponse> UpdateCategoryAsync(Guid id, CategoryWrite request, CancellationToken ct);
    Task<MemberCategoryResponse> CategoryAsync(Guid memberId, CancellationToken ct);
    Task<MemberCategoryResponse> AssignCategoryAsync(Guid memberId, MemberCategoryUpdate request, CancellationToken ct);
    Task<Page<MemberHistoryEntry>> HistoryAsync(Guid memberId, ListQuery query, CancellationToken ct);
}

public static class AdministratorValidation
{
    public const string PasswordGuidance = "Use 8 to 128 characters, including an ASCII digit and a special character (not a letter, number, or whitespace).";
    public static void Email(string? email) => Validation.Login(new(email, "validation-only"));
    public static void Password(string? password)
    {
        if (password is null || password.EnumerateRunes().Count() is < 8 or > 128 ||
            !password.Any(c => c is >= '0' and <= '9') ||
            !password.EnumerateRunes().Any(r => !System.Text.Rune.IsLetter(r) &&
                System.Text.Rune.GetUnicodeCategory(r) is not (System.Globalization.UnicodeCategory.DecimalDigitNumber or System.Globalization.UnicodeCategory.LetterNumber or System.Globalization.UnicodeCategory.OtherNumber) &&
                !System.Text.Rune.IsWhiteSpace(r)))
            throw new RequestFailure(422, PasswordGuidance, new() { ["password"] = [PasswordGuidance] });
    }
    public static void Role(string? role)
    {
        if (role is not (nameof(AdminRole.TenantAdmin) or nameof(AdminRole.TenantStaff)))
            throw new RequestFailure(422, "Choose a tenant role.", new() { ["role"] = ["Choose TenantAdmin or TenantStaff."] });
    }
}
