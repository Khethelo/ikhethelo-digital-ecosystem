using System.Data;
using IKhethelo.Digital.Application;
using IKhethelo.Digital.Domain;
using IKhethelo.Digital.Infrastructure.Audit;
using IKhethelo.Digital.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IKhethelo.Digital.Infrastructure.Ntm;

public sealed class NtmOperations(DigitalDbContext db, ITenantContext tenant, AuditWriter audit, TimeProvider clock) : INtmOperations
{
    private IQueryable<NtmVeteran> Veterans => db.NtmVeterans.Where(x => x.TenantId == tenant.Current.TenantId);
    private IQueryable<NtmBusiness> Businesses => db.NtmBusinesses.Where(x => x.TenantId == tenant.Current.TenantId);
    private static string? Clean(string? value) => NtmValidation.Clean(value);
    private static NtmVeteranResponse Response(NtmVeteran v) => new(v.Id, v.FullName, v.MkmvaNumber, v.DmvNumber,
        v.ContactNumber, v.CreatedAtUtc, v.UpdatedAtUtc, v.Business is null ? null : new(v.Business.Id, v.Business.BusinessName),
        v.EmailAddress, v.MembershipReference);
    private static NtmBusinessResponse Response(NtmBusiness b) => new(b.Id, b.VeteranId, b.BusinessName,
        b.CipcRegistrationNumber, b.BusinessAddress, b.Sector, b.BusinessBackground, b.OperationalStatus,
        b.NumberOfEmployees, b.YearsInOperation, b.CurrentBusinessNeeds, b.PriorityNeed, b.FundingRequired,
        b.FundingAmount, b.AdditionalComments, b.AssessmentDate, b.AssessedBy, b.CreatedAtUtc, b.UpdatedAtUtc);
    public async Task<Page<NtmVeteranResponse>> VeteransAsync(NtmListQuery query, CancellationToken ct)
    {
        NtmValidation.Query(query); var rows = Veterans.AsNoTracking();
        if (Clean(query.Search) is { } search)
        {
            var normalized = search.ToUpperInvariant();
            rows = rows.Where(x => x.FullName.ToUpper().Contains(normalized) ||
                (x.MkmvaNumber != null && x.MkmvaNumber.ToUpper().Contains(normalized)) ||
                (x.DmvNumber != null && x.DmvNumber.ToUpper().Contains(normalized)));
        }
        if (query.HasBusiness is { } hasBusiness) rows = rows.Where(x => (x.Business != null) == hasBusiness);
        var count = await rows.LongCountAsync(ct);
        var items = await rows.Include(x => x.Business).OrderBy(x => x.FullName).ThenBy(x => x.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        await audit.ReadAsync("NtmVeteran", null, ct);
        return new(items.Select(Response).ToArray(), query.Page, query.PageSize, count);
    }
    public async Task<NtmVeteranResponse> VeteranAsync(Guid id, CancellationToken ct)
    {
        var row = await Veterans.AsNoTracking().Include(x => x.Business).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw RequestFailure.NotFound();
        await audit.ReadAsync("NtmVeteran", id, ct); return Response(row);
    }
    public async Task<NtmVeteranResponse> CreateVeteranAsync(NtmVeteranWrite request, CancellationToken ct)
    {
        NtmValidation.Veteran(request);
        var row = new NtmVeteran { Id = Guid.NewGuid(), TenantId = tenant.Current.TenantId, CreatedAtUtc = clock.GetUtcNow() };
        Assign(row, request); db.NtmVeterans.Add(row);
        await SaveAsync("NtmVeteranCreated", "NtmVeteran", row.Id, ct); return Response(row);
    }
    public async Task<NtmVeteranResponse> UpdateVeteranAsync(Guid id, NtmVeteranWrite request, CancellationToken ct)
    {
        NtmValidation.Veteran(request);
        var row = await Veterans.Include(x => x.Business).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw RequestFailure.NotFound();
        Assign(row, request); await SaveAsync("NtmVeteranUpdated", "NtmVeteran", id, ct); return Response(row);
    }
    private void Assign(NtmVeteran row, NtmVeteranWrite request)
    {
        row.FullName = Clean(request.FullName)!; row.MkmvaNumber = Clean(request.MkmvaNumber);
        row.DmvNumber = Clean(request.DmvNumber); row.ContactNumber = Clean(request.ContactNumber); row.UpdatedAtUtc = clock.GetUtcNow();
        row.EmailAddress = Clean(request.EmailAddress); row.MembershipReference = request.MembershipReference;
    }
    public async Task<Page<NtmBusinessResponse>> BusinessesAsync(NtmListQuery query, CancellationToken ct)
    {
        NtmValidation.Query(query); var rows = Businesses.AsNoTracking();
        if (Clean(query.Search) is { } search) { var normalized = search.ToUpperInvariant(); rows = rows.Where(x => x.BusinessName.ToUpper().Contains(normalized)); }
        if (Clean(query.Sector) is { } sector) rows = rows.Where(x => x.Sector == sector);
        if (Clean(query.OperationalStatus) is { } status) rows = rows.Where(x => x.OperationalStatus == status);
        if (query.VeteranId is { } veteranId) rows = rows.Where(x => x.VeteranId == veteranId);
        var count = await rows.LongCountAsync(ct);
        var items = await rows.OrderBy(x => x.BusinessName).ThenBy(x => x.Id).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(ct);
        await audit.ReadAsync("NtmBusiness", null, ct);
        return new(items.Select(Response).ToArray(), query.Page, query.PageSize, count);
    }
    public async Task<NtmBusinessResponse> BusinessAsync(Guid id, CancellationToken ct)
    {
        var row = await Businesses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw RequestFailure.NotFound();
        await audit.ReadAsync("NtmBusiness", id, ct); return Response(row);
    }
    public async Task<NtmBusinessResponse> CreateBusinessAsync(NtmBusinessWrite request, CancellationToken ct)
    {
        NtmValidation.Business(request);
        if (!await Veterans.AnyAsync(x => x.Id == request.VeteranId, ct)) throw RequestFailure.NotFound();
        var row = new NtmBusiness { Id = Guid.NewGuid(), TenantId = tenant.Current.TenantId, CreatedAtUtc = clock.GetUtcNow() };
        Assign(row, request); db.NtmBusinesses.Add(row);
        await SaveAsync("NtmBusinessCreated", "NtmBusiness", row.Id, ct); return Response(row);
    }
    public async Task<NtmBusinessResponse> UpdateBusinessAsync(Guid id, NtmBusinessWrite request, CancellationToken ct)
    {
        NtmValidation.Business(request);
        var row = await Businesses.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw RequestFailure.NotFound();
        if (!await Veterans.AnyAsync(x => x.Id == request.VeteranId, ct)) throw RequestFailure.NotFound();
        Assign(row, request); await SaveAsync("NtmBusinessUpdated", "NtmBusiness", id, ct); return Response(row);
    }
    private void Assign(NtmBusiness row, NtmBusinessWrite r)
    {
        row.VeteranId = r.VeteranId; row.BusinessName = Clean(r.BusinessName)!;
        row.CipcRegistrationNumber = Clean(r.CipcRegistrationNumber); row.BusinessAddress = Clean(r.BusinessAddress);
        row.Sector = Clean(r.Sector); row.BusinessBackground = Clean(r.BusinessBackground);
        row.OperationalStatus = Clean(r.OperationalStatus); row.NumberOfEmployees = r.NumberOfEmployees;
        row.YearsInOperation = r.YearsInOperation; row.CurrentBusinessNeeds = Clean(r.CurrentBusinessNeeds);
        row.PriorityNeed = Clean(r.PriorityNeed); row.FundingRequired = r.FundingRequired; row.FundingAmount = r.FundingAmount;
        row.AdditionalComments = Clean(r.AdditionalComments); row.AssessmentDate = r.AssessmentDate;
        row.AssessedBy = Clean(r.AssessedBy); row.UpdatedAtUtc = clock.GetUtcNow();
    }
    private async Task SaveAsync(string action, string entity, Guid id, CancellationToken ct)
    {
        audit.Add(action, entity, id);
        // SaveChanges commits entity and required audit in the same transaction.
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_ntm_veterans_tenant_mkmva" or "ux_ntm_veterans_tenant_dmv" or "ux_ntm_businesses_tenant_veteran" })
        { throw RequestFailure.Conflict(); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.ForeignKeyViolation, ConstraintName: "fk_ntm_businesses_veteran_tenant" })
        { throw RequestFailure.NotFound(); }
    }
    public async Task<NtmDashboard> DashboardAsync(CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var totalVeterans = await Veterans.CountAsync(ct);
        var stats = await Businesses.GroupBy(x => 1).Select(g => new
        {
            Total = g.Count(), Operational = g.Count(x => x.OperationalStatus == "Operational"),
            Partial = g.Count(x => x.OperationalStatus == "Partially Operational"),
            NotOperational = g.Count(x => x.OperationalStatus == "Not Operational"),
            Amount = g.Sum(x => x.FundingAmount ?? 0), Funding = g.Count(x => x.FundingRequired == true)
        }).SingleOrDefaultAsync(ct);
        var sectors = await Businesses.Where(x => x.Sector != null).GroupBy(x => x.Sector!)
            .OrderBy(g => g.Key).Select(g => new NtmCount(g.Key, g.Count())).ToListAsync(ct);
        var needs = await Businesses.Where(x => x.PriorityNeed != null).GroupBy(x => x.PriorityNeed!)
            .OrderBy(g => g.Key).Select(g => new NtmCount(g.Key, g.Count())).ToListAsync(ct);
        await audit.ReadAsync("NtmDashboard", null, ct); await transaction.CommitAsync(ct);
        return new(totalVeterans, stats?.Total ?? 0, stats?.Operational ?? 0, stats?.Partial ?? 0,
            stats?.NotOperational ?? 0, stats?.Amount ?? 0, stats?.Funding ?? 0, sectors, needs);
    }
}
