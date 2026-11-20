using System.Globalization;
using IKhethelo.Digital.Application;
using IKhethelo.Digital.Domain;
using IKhethelo.Digital.Infrastructure.Audit;
using IKhethelo.Digital.Infrastructure.Persistence;
using IKhethelo.Digital.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace IKhethelo.Digital.Infrastructure.Membership;

public sealed class ReviewApplications(DigitalDbContext db, TenantData data, AuditWriter audit, TimeProvider clock, MembershipRules rules, MembershipEmails emails) : IReviewApplications
{
    private async Task<MembershipApplication> LockPendingAsync(Guid id, CancellationToken ct)
    {
        var rows = await db.Applications.FromSqlInterpolated($"SELECT * FROM membership_applications WHERE id = {id} AND tenant_id = {data.Access.TenantId} FOR UPDATE").ToListAsync(ct);
        var application = rows.SingleOrDefault() ?? throw RequestFailure.NotFound();
        if (application.Status != MembershipApplicationStatus.AwaitingInPersonCompletion) throw RequestFailure.Conflict();
        return application;
    }
    public async Task<MemberResponse> ActivateAsync(Guid id, ReviewRequest request, CancellationToken ct) =>
        (await ApproveAsync(id, request, ct)).Member;
    public async Task<MembershipApprovalResponse> ApproveAsync(Guid id, ReviewRequest request, CancellationToken ct)
    {
        Validation.Review(request);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var application = await LockPendingAsync(id, ct);
        if (await data.Members.AnyAsync(x => x.Status == MemberStatus.Active &&
            (x.NormalizedEmail == application.NormalizedEmail || x.NormalizedPhoneNumber == application.NormalizedPhoneNumber), ct)) throw RequestFailure.Conflict();
        // Prefix selection is tenant configuration/business policy, not a universal ESC assumption.
        var prefix = rules.For(data.Access.TenantCode).MemberNumberPrefix ?? data.Access.TenantCode;
        var number = await db.Database.SqlQueryRaw<long>("SELECT nextval('member_number_seq') AS \"Value\"").SingleAsync(ct);
        var now = clock.GetUtcNow();
        var member = new Member
        {
            Id = Guid.NewGuid(), TenantId = data.Access.TenantId, ApplicationId = application.Id,
            MemberNumber = prefix + "-" + number.ToString("D5", CultureInfo.InvariantCulture),
            FirstName = application.FirstName, LastName = application.LastName, DateOfBirth = application.DateOfBirth,
            PhoneNumber = application.PhoneNumber, NormalizedPhoneNumber = application.NormalizedPhoneNumber,
            Email = application.Email, NormalizedEmail = application.NormalizedEmail, Status = MemberStatus.Active,
            JoinedAtUtc = now, UpdatedAtUtc = now
        };
        db.Members.Add(member); Review(application, MembershipApplicationStatus.Completed, request, now);
        audit.Add("MembershipActivated", "MembershipApplication", id);
        audit.Add("RegisterMember", "Member", member.Id, new { status = "Active" });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        var emailSent = await emails.ActivatedAsync(member.Email, member.FirstName, member.MemberNumber);
        return new(member.Response(), emailSent);
    }
    public async Task<ApplicationResponse> DeclineAsync(Guid id, ReviewRequest request, CancellationToken ct)
    {
        Validation.Review(request);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var application = await LockPendingAsync(id, ct);
        Review(application, MembershipApplicationStatus.LegacyClosed, request, clock.GetUtcNow());
        audit.Add("MembershipApplicationRejected", "MembershipApplication", id);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return application.Response();
    }
    private void Review(MembershipApplication application, MembershipApplicationStatus status, ReviewRequest request, DateTimeOffset now)
    {
        application.Status = status; application.ReviewedAtUtc = now; application.UpdatedAtUtc = now;
        application.ReviewedByAdminId = data.Access.ActorId ?? throw new RequestFailure(403, "Administrator access is required.");
        application.ReviewNotes = request.ReviewNotes;
    }
}
