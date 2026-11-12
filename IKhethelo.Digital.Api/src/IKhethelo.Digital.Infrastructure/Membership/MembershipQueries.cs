using IKhethelo.Digital.Application;
using IKhethelo.Digital.Domain;
using IKhethelo.Digital.Infrastructure.Audit;
using IKhethelo.Digital.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace IKhethelo.Digital.Infrastructure.Membership;

public sealed class MembershipQueries(TenantData data, AuditWriter audit) : IMembershipQueries
{
    public async Task<Page<ApplicationResponse>> ApplicationsAsync(ListQuery query, CancellationToken ct)
    {
        Validation.Query<MembershipApplicationStatus>(query);
        var rows = data.Applications.AsNoTracking();
        if (query.Status is not null) { var status = Enum.Parse<MembershipApplicationStatus>(query.Status); rows = rows.Where(x => x.Status == status); }
        if (query.Search?.Trim() is { Length: > 0 } search)
        {
            var normalized = Validation.NormalizeEmail(search);
            rows = rows.Where(x => x.FirstName.ToUpper().Contains(normalized) || x.LastName.ToUpper().Contains(normalized) ||
                x.NormalizedEmail.Contains(normalized) || x.NormalizedPhoneNumber.Contains(search));
        }
        var count = await rows.LongCountAsync(ct);
        // A long offset avoids integer overflow; empty pages need no database offset.
        var offset = ((long)query.Page - 1) * query.PageSize;
        var items = offset >= count ? [] : await rows.OrderByDescending(x => x.SubmittedAtUtc).ThenBy(x => x.Id)
            .Skip((int)offset).Take(query.PageSize).ToListAsync(ct);
        await audit.ReadAsync("MembershipApplication", null, ct);
        return new(items.Select(x => x.Response()).ToArray(), query.Page, query.PageSize, count);
    }
    public async Task<ApplicationResponse> ApplicationAsync(Guid id, CancellationToken ct)
    {
        var row = await data.Applications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw RequestFailure.NotFound();
        await audit.ReadAsync("MembershipApplication", id, ct); return row.Response();
    }
    public async Task<Page<MemberResponse>> MembersAsync(ListQuery query, CancellationToken ct)
    {
        Validation.Query<MemberStatus>(query);
        var rows = data.Members.AsNoTracking();
        if (query.Status is not null) { var status = Enum.Parse<MemberStatus>(query.Status); rows = rows.Where(x => x.Status == status); }
        if (query.Search?.Trim() is { Length: > 0 } search)
        {
            var normalized = Validation.NormalizeEmail(search);
            rows = rows.Where(x => x.FirstName.ToUpper().Contains(normalized) || x.LastName.ToUpper().Contains(normalized) ||
                x.NormalizedEmail.Contains(normalized) || x.NormalizedPhoneNumber.Contains(search) || x.MemberNumber.ToUpper().Contains(normalized));
        }
        var count = await rows.LongCountAsync(ct); var offset = ((long)query.Page - 1) * query.PageSize;
        var items = offset >= count ? [] : await rows.OrderByDescending(x => x.JoinedAtUtc).ThenBy(x => x.Id)
            .Skip((int)offset).Take(query.PageSize).ToListAsync(ct);
        await audit.ReadAsync("Member", null, ct);
        return new(items.Select(x => x.Response()).ToArray(), query.Page, query.PageSize, count);
    }
    public async Task<MemberResponse> MemberAsync(Guid id, CancellationToken ct)
    {
        var row = await data.Members.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw RequestFailure.NotFound();
        await audit.ReadAsync("Member", id, ct); return row.Response();
    }
}
