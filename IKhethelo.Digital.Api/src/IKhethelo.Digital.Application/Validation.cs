using System.Net.Mail;
using System.Text.RegularExpressions;
using IKhethelo.Digital.Domain;

namespace IKhethelo.Digital.Application;

public static partial class Validation
{
    public static string NormalizeEmail(string value) => value.Trim().ToUpperInvariant();
    public static string NormalizePhone(string value, bool localPhone) => localPhone && LocalPhonePattern().IsMatch(value) ? "+27" + value[1..] : value;
    public static string DisplayPhone(string value, bool localPhone) => localPhone && SouthAfricanPhonePattern().IsMatch(value) ? "0" + value[3..] : value;
    public static DateOnly LocalDate(TimeProvider clock) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg")).DateTime);
    public static bool IsAdult(DateOnly birth, DateOnly today) => birth <= today && birth.Year <= today.Year - 18 && birth.AddYears(18) <= today;
    private static int Length(string s) => s.EnumerateRunes().Count();
    private static void Name(Dictionary<string, string[]> errors, string key, string? value)
    {
        if (value is null || Length(value.Trim()) is < 2 or > 100) errors[key] = ["Use 2 to 100 characters after trimming."];
    }
    private static void Email(Dictionary<string, string[]> errors, string? value)
    {
        var email = value?.Trim();
        if (string.IsNullOrEmpty(email) || Length(email) > 320 || Length(NormalizeEmail(email)) > 320 ||
            !MailAddress.TryCreate(email, out var address) || address.Address != email || !email.Contains('@'))
            errors["email"] = ["Enter a valid email of at most 320 characters."];
    }
    private static void Contacts(Dictionary<string, string[]> errors, string? first, string? last, string? phone, string? email, bool localPhone)
    {
        Name(errors, "firstName", first); Name(errors, "lastName", last); Email(errors, email);
        if (phone is null || !(PhonePattern().IsMatch(phone) || (localPhone && LocalPhonePattern().IsMatch(phone)))) errors["phoneNumber"] = [localPhone ? "Enter a valid phone number, for example 0843190994." : "Use an international phone number, for example +27821234567."];
    }
    public static void Submission(ApplicationSubmission request, DateOnly today, bool requireAdult, bool localPhone = false)
    {
        var errors = new Dictionary<string, string[]>();
        Contacts(errors, request.FirstName, request.LastName, request.PhoneNumber, request.Email, localPhone);
        if (request.DateOfBirth is not { } birth || birth > today || (requireAdult && !IsAdult(birth, today)))
            errors["dateOfBirth"] = ["Enter a valid birth date; Es'hlahleni applicants must be at least 18."];
        if (request.TermsAccepted != true) errors["termsAccepted"] = ["Terms must be accepted."];
        if (request.PrivacyConsent != true) errors["privacyConsent"] = ["Privacy consent is required."];
        Throw(errors);
    }
    public static void Member(MemberUpdate request, bool localPhone = false)
    {
        var errors = new Dictionary<string, string[]>();
        Contacts(errors, request.FirstName, request.LastName, request.PhoneNumber, request.Email, localPhone); Throw(errors);
    }
    public static void Login(LoginRequest request)
    {
        var errors = new Dictionary<string, string[]>(); Email(errors, request.Email);
        if (request.Password is null || Length(request.Password) is < 1 or > 1024) errors["password"] = ["A password of 1 to 1024 characters is required."];
        Throw(errors);
    }
    public static void Review(ReviewRequest request)
    {
        if (request.ReviewNotes is { } notes && Length(notes) is < 1 or > 2000)
            Throw(new() { ["reviewNotes"] = ["Use 1 to 2000 characters, or omit reviewNotes."] });
    }
    public static void Status(MemberStatusUpdate request)
    {
        if (request.Status is null || !Enum.IsDefined(request.Status.Value))
            Throw(new() { ["status"] = ["Choose Active, Suspended or Inactive."] });
    }
    public static void Query<TEnum>(ListQuery query) where TEnum : struct, Enum
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (query.Search is { } search && Length(search) > 320) ||
            (query.Status is not null && (!Enum.TryParse<TEnum>(query.Status, false, out var parsed) || !Enum.IsDefined(parsed) || parsed.ToString() != query.Status)))
            throw new RequestFailure(400, "Invalid paging, search or status query.");
    }
    private static void Throw(Dictionary<string, string[]> errors)
    {
        if (errors.Count != 0) throw new RequestFailure(422, "One or more validation errors occurred.", errors);
    }
    [GeneratedRegex(@"\A\+[1-9][0-9]{7,14}\z", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();
    [GeneratedRegex(@"\A0[1-9][0-9]{8}\z", RegexOptions.CultureInvariant)]
    private static partial Regex LocalPhonePattern();
    [GeneratedRegex(@"\A\+27[1-9][0-9]{8}\z", RegexOptions.CultureInvariant)]
    private static partial Regex SouthAfricanPhonePattern();
}
