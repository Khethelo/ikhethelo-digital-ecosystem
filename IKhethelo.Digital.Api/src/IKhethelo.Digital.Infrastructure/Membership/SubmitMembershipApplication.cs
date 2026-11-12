using IKhethelo.Digital.Application;
using IKhethelo.Digital.Domain;
using IKhethelo.Digital.Infrastructure.Persistence;
using IKhethelo.Digital.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace IKhethelo.Digital.Infrastructure.Membership;

public sealed class SubmitMembershipApplication(DigitalDbContext db, TenantData data, TimeProvider clock, MembershipRules rules, MembershipEmails emails, IKhethelo.Digital.Infrastructure.Audit.AuditWriter audit) : ISubmitMembershipApplication
{
    public async Task<SubmissionReceipt> ExecuteAsync(ApplicationSubmission request, CancellationToken ct)
    {
        Validation.Submission(request, Validation.LocalDate(clock), rules.For(data.Access.TenantCode).RequireAdult);
        var email = request.Email!.Trim(); var normalized = Validation.NormalizeEmail(email);
        if (await data.Applications.AnyAsync(x => x.Status == MembershipApplicationStatus.AwaitingInPersonCompletion &&
                (x.NormalizedEmail == normalized || x.NormalizedPhoneNumber == request.PhoneNumber), ct)) throw RequestFailure.Conflict();
        if (await data.Members.AnyAsync(x => x.Status == MemberStatus.Active && (x.NormalizedEmail == normalized || x.NormalizedPhoneNumber == request.PhoneNumber), ct)) throw RequestFailure.Conflict();
        var now = clock.GetUtcNow();
        var application = new MembershipApplication
        {
            Id = Guid.NewGuid(), TenantId = data.Access.TenantId, FirstName = request.FirstName!.Trim(), LastName = request.LastName!.Trim(),
            DateOfBirth = request.DateOfBirth!.Value, PhoneNumber = request.PhoneNumber!, NormalizedPhoneNumber = request.PhoneNumber!,
            Email = email, NormalizedEmail = normalized, TermsAccepted = true, PrivacyConsent = true,
            Status = MembershipApplicationStatus.AwaitingInPersonCompletion, SubmittedAtUtc = now, UpdatedAtUtc = now
        };
        db.Applications.Add(application);
        audit.Add("RegistrationCreated", "MembershipApplication", application.Id);
        await db.SaveChangesAsync(ct);
        await emails.ReceivedAsync(application.Email, application.FirstName, application.Id, application.SubmittedAtUtc);
        return new(application.Id, application.Status, application.SubmittedAtUtc, "Your registration has been received.");
    }
}
