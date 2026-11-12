using IKhethelo.Digital.Application;
using IKhethelo.Digital.Domain;
using IKhethelo.Digital.Infrastructure.Audit;
using IKhethelo.Digital.Infrastructure.Persistence;
using IKhethelo.Digital.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace IKhethelo.Digital.Infrastructure.Membership;

public sealed class MemberAdministration(DigitalDbContext db, TenantData data, AuditWriter audit, TimeProvider clock) : IMemberAdministration
{
    private async Task<Member> LockAsync(Guid id, CancellationToken ct) => (await db.Members.FromSqlInterpolated(
        $"SELECT * FROM members WHERE id = {id} AND tenant_id = {data.Access.TenantId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault() ?? throw RequestFailure.NotFound();
    private async Task CheckAsync(Member member, CancellationToken ct)
    {
        if (member.Status == MemberStatus.Active && await data.Members.AnyAsync(x => x.Id != member.Id && x.Status == MemberStatus.Active &&
            (x.NormalizedEmail == member.NormalizedEmail || x.NormalizedPhoneNumber == member.NormalizedPhoneNumber), ct)) throw RequestFailure.Conflict();
    }
    public async Task<MemberResponse> UpdateAsync(Guid id, MemberUpdate request, CancellationToken ct)
    {
        Validation.Member(request);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var member = await LockAsync(id, ct);
        member.FirstName = request.FirstName!.Trim(); member.LastName = request.LastName!.Trim();
        member.Email = request.Email!.Trim(); member.NormalizedEmail = Validation.NormalizeEmail(member.Email);
        member.PhoneNumber = request.PhoneNumber!; member.NormalizedPhoneNumber = request.PhoneNumber!;
        await CheckAsync(member, ct); member.UpdatedAtUtc = clock.GetUtcNow();
        audit.Add("UpdateMember", "Member", id);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return member.Response();
    }
    public async Task<MemberResponse> StatusAsync(Guid id, MemberStatusUpdate request, CancellationToken ct)
    {
        Validation.Status(request);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var member = await LockAsync(id, ct);
        if (member.Status != request.Status)
        {
            var previous = member.Status;
            member.Status = request.Status!.Value; await CheckAsync(member, ct); member.UpdatedAtUtc = clock.GetUtcNow();
            audit.Add("UpdateMemberStatus", "Member", id, new { previousStatus = previous.ToString(), status = member.Status.ToString() });
        }
        else if (data.Access.IsInternal) audit.Add("CrossTenantRead", "Member", id);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return member.Response();
    }
}
