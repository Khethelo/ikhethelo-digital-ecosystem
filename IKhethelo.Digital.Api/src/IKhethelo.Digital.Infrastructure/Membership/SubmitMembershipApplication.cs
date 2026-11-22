using IKhethelo.Digital.Application;
using IKhethelo.Digital.Domain;
using IKhethelo.Digital.Infrastructure.Persistence;
using IKhethelo.Digital.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace IKhethelo.Digital.Infrastructure.Membership;

public sealed class SubmitMembershipApplication(DigitalDbContext db, TenantData data, TimeProvider clock, MembershipRules rules, MembershipEmails emails, IKhethelo.Digital.Infrastructure.Audit.AuditWriter audit) : ISubmitMembershipApplication
{
    public async Task<SubmissionReceipt?> ExecuteAsync(ApplicationSubmission request, CancellationToken ct)
    {
        var localPhone = data.Access.TenantCode == "ESHL";
        Validation.Submission(request, Validation.LocalDate(clock), rules.For(data.Access.TenantCode).RequireAdult, localPhone);
        var email = request.Email!.Trim(); var normalized = Validation.NormalizeEmail(email);
        var phone = Validation.NormalizePhone(request.PhoneNumber!, localPhone);
        if (await data.Applications.AnyAsync(x => x.Status == MembershipApplicationStatus.Pending &&
                (x.NormalizedEmail == normalized || x.NormalizedPhoneNumber == phone), ct)) return null;
        if (await data.Members.AnyAsync(x => x.Status == MemberStatus.Active && (x.NormalizedEmail == normalized || x.NormalizedPhoneNumber == phone), ct)) return null;
        var now = clock.GetUtcNow();
        var application = new MembershipApplication
        {
            Id = Guid.NewGuid(), TenantId = data.Access.TenantId, FirstName = request.FirstName!.Trim(), LastName = request.LastName!.Trim(),
            DateOfBirth = request.DateOfBirth!.Value, PhoneNumber = Validation.DisplayPhone(request.PhoneNumber!, localPhone), NormalizedPhoneNumber = phone,
            Email = email, NormalizedEmail = normalized, TermsAccepted = true, PrivacyConsent = true,
            Status = MembershipApplicationStatus.Pending, SubmittedAtUtc = now, UpdatedAtUtc = now
        };
        db.Applications.Add(application);
        audit.Add("RegistrationCreated", "MembershipApplication", application.Id);
        await db.SaveChangesAsync(ct);
        await emails.ReceivedAsync(application.Email, application.FirstName, application.Id, application.SubmittedAtUtc);
        return new(application.Id, application.Status, application.SubmittedAtUtc, "Your application has been submitted and is pending review.");
    }
}
