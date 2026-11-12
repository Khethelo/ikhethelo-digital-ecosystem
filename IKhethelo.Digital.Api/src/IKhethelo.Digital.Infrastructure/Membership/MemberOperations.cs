using System.Globalization;
using System.Text.Json;
using IKhethelo.Digital.Application;
using IKhethelo.Digital.Domain;
using IKhethelo.Digital.Infrastructure.Audit;
using IKhethelo.Digital.Infrastructure.Persistence;
using IKhethelo.Digital.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace IKhethelo.Digital.Infrastructure.Membership;

public sealed class MemberOperations(DigitalDbContext db, TenantData data, AuditWriter audit, TimeProvider clock,
    MembershipRules rules, MembershipEmails emails) : IMemberOperations
{
    private IQueryable<MembershipCategory> Categories => db.MembershipCategories.Where(x => x.TenantId == data.Access.TenantId);
    private async Task<MembershipCategory?> AvailableCategoryAsync(Guid? id, CancellationToken ct)
    {
        if (id is null) return null;
        var category = (await db.MembershipCategories.FromSqlInterpolated(
            $"SELECT * FROM membership_categories WHERE id = {id.Value} AND tenant_id = {data.Access.TenantId} FOR SHARE").ToListAsync(ct)).SingleOrDefault();
        if (category is null) throw RequestFailure.NotFound();
        if (!category.IsActive) throw new RequestFailure(422, "Choose an active membership category.", new() { ["categoryId"] = ["The category is inactive."] });
        return category;
    }
    public async Task<MemberResponse> RegisterAsync(MemberRegistration request, CancellationToken ct)
    {
        Validation.Submission(new(request.FirstName, request.LastName, request.DateOfBirth, request.PhoneNumber,
            request.Email, request.TermsAccepted, request.PrivacyConsent), Validation.LocalDate(clock), rules.For(data.Access.TenantCode).RequireAdult);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var category = await AvailableCategoryAsync(request.CategoryId, ct);
        var email = request.Email!.Trim(); var normalized = Validation.NormalizeEmail(email);
        if (await data.Applications.AnyAsync(x => x.Status == MembershipApplicationStatus.AwaitingInPersonCompletion &&
            (x.NormalizedEmail == normalized || x.NormalizedPhoneNumber == request.PhoneNumber), ct) ||
            await data.Members.AnyAsync(x => x.Status == MemberStatus.Active &&
            (x.NormalizedEmail == normalized || x.NormalizedPhoneNumber == request.PhoneNumber), ct)) throw RequestFailure.Conflict();
        var now = clock.GetUtcNow(); var actor = data.Access.ActorId ?? throw new RequestFailure(403, "Administrator access is required.");
        var application = new MembershipApplication { Id = Guid.NewGuid(), TenantId = data.Access.TenantId,
            FirstName = request.FirstName!.Trim(), LastName = request.LastName!.Trim(), DateOfBirth = request.DateOfBirth!.Value,
            PhoneNumber = request.PhoneNumber!, NormalizedPhoneNumber = request.PhoneNumber!, Email = email, NormalizedEmail = normalized,
            TermsAccepted = true, PrivacyConsent = true, Status = MembershipApplicationStatus.Completed,
            SubmittedAtUtc = now, UpdatedAtUtc = now, ReviewedAtUtc = now, ReviewedByAdminId = actor,
            ReviewNotes = "Registered directly by an authorized administrator." };
        var number = await db.Database.SqlQueryRaw<long>("SELECT nextval('member_number_seq') AS \"Value\"").SingleAsync(ct);
        var prefix = rules.For(data.Access.TenantCode).MemberNumberPrefix ?? data.Access.TenantCode;
        var member = new Member { Id = Guid.NewGuid(), TenantId = data.Access.TenantId, ApplicationId = application.Id,
            MemberNumber = prefix + "-" + number.ToString("D5", CultureInfo.InvariantCulture), FirstName = application.FirstName,
            LastName = application.LastName, DateOfBirth = application.DateOfBirth, Email = email, NormalizedEmail = normalized,
            PhoneNumber = application.PhoneNumber, NormalizedPhoneNumber = application.NormalizedPhoneNumber,
            Status = MemberStatus.Active, JoinedAtUtc = now, UpdatedAtUtc = now, CategoryId = category?.Id };
        db.Applications.Add(application); db.Members.Add(member);
        audit.Add("RegisterMember", "Member", member.Id, new { status = "Active", categoryId = member.CategoryId });
        audit.Add("MembershipActivated", "MembershipApplication", application.Id);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        await emails.ActivatedAsync(member.Email, member.FirstName, member.MemberNumber);
        return member.Response();
    }
    private static CategoryResponse Response(MembershipCategory category) => new(category.Id, category.Name, category.IsActive);
    private static string CategoryName(CategoryWrite request)
    {
        var name = request.Name?.Trim();
        if (name is null || name.EnumerateRunes().Count() is < 2 or > 100 || name.ToUpperInvariant().EnumerateRunes().Count() > 100 || request.IsActive is null)
            throw new RequestFailure(422, "Supply a category name of 2 to 100 characters and isActive.",
                new() { ["category"] = ["A name of 2 to 100 characters and isActive are required."] });
        return name;
    }
    public async Task<Page<CategoryResponse>> CategoriesAsync(ListQuery query, CancellationToken ct)
    {
        Validation.Query<MemberStatus>(query with { Status = null });
        if (query.Status is not (null or "Active" or "Inactive")) throw new RequestFailure(400, "Choose category status Active or Inactive.");
        var rows = Categories.AsNoTracking();
        if (query.Status is { } status) rows = rows.Where(x => x.IsActive == (status == "Active"));
        if (query.Search?.Trim() is { Length: > 0 } search) { var normalized = search.ToUpperInvariant(); rows = rows.Where(x => x.NormalizedName.Contains(normalized)); }
        var count = await rows.LongCountAsync(ct); var offset = ((long)query.Page - 1) * query.PageSize;
        var items = offset >= count ? [] : await rows.OrderBy(x => x.NormalizedName).ThenBy(x => x.Id).Skip((int)offset).Take(query.PageSize).ToListAsync(ct);
        await audit.ReadAsync("MembershipCategory", null, ct);
        return new(items.Select(Response).ToArray(), query.Page, query.PageSize, count);
    }
    public async Task<CategoryResponse> CreateCategoryAsync(CategoryWrite request, CancellationToken ct)
    {
        var name = CategoryName(request); var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var category = new MembershipCategory { Id = Guid.NewGuid(), TenantId = data.Access.TenantId, Name = name,
            NormalizedName = name.ToUpperInvariant(), IsActive = request.IsActive!.Value, CreatedAtUtc = now, UpdatedAtUtc = now };
        db.MembershipCategories.Add(category); audit.Add("CreateMembershipCategory", "MembershipCategory", category.Id);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Response(category);
    }
    public async Task<CategoryResponse> UpdateCategoryAsync(Guid id, CategoryWrite request, CancellationToken ct)
    {
        var name = CategoryName(request);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var category = (await db.MembershipCategories.FromSqlInterpolated(
            $"SELECT * FROM membership_categories WHERE id = {id} AND tenant_id = {data.Access.TenantId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault() ?? throw RequestFailure.NotFound();
        category.Name = name; category.NormalizedName = name.ToUpperInvariant(); category.IsActive = request.IsActive!.Value; category.UpdatedAtUtc = clock.GetUtcNow();
        audit.Add("UpdateMembershipCategory", "MembershipCategory", id);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return Response(category);
    }
    public async Task<MemberCategoryResponse> CategoryAsync(Guid memberId, CancellationToken ct)
    {
        var member = await data.Members.AsNoTracking().Include(x => x.Category).SingleOrDefaultAsync(x => x.Id == memberId, ct) ?? throw RequestFailure.NotFound();
        await audit.ReadAsync("Member", memberId, ct); return new(member.Id, member.CategoryId, member.Category?.Name);
    }
    public async Task<MemberCategoryResponse> AssignCategoryAsync(Guid memberId, MemberCategoryUpdate request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var member = (await db.Members.FromSqlInterpolated(
            $"SELECT * FROM members WHERE id = {memberId} AND tenant_id = {data.Access.TenantId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault() ?? throw RequestFailure.NotFound();
        var category = await AvailableCategoryAsync(request.CategoryId, ct); var previous = member.CategoryId;
        member.CategoryId = category?.Id; member.UpdatedAtUtc = clock.GetUtcNow();
        audit.Add("SetMemberCategory", "Member", memberId, new { previousCategoryId = previous, categoryId = member.CategoryId });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return new(member.Id, member.CategoryId, category?.Name);
    }
    public async Task<Page<MemberHistoryEntry>> HistoryAsync(Guid memberId, ListQuery query, CancellationToken ct)
    {
        Validation.Query<MemberStatus>(query);
        var member = await data.Members.AsNoTracking().SingleOrDefaultAsync(x => x.Id == memberId, ct) ?? throw RequestFailure.NotFound();
        var rows = db.AuditLogs.AsNoTracking().Where(x => x.TenantId == data.Access.TenantId &&
            ((x.EntityType == "Member" && x.EntityId == memberId && x.Action != "CrossTenantRead") ||
             (x.EntityType == "MembershipApplication" && x.EntityId == member.ApplicationId && (x.Action == "MembershipActivated" || x.Action == "ApproveMembershipApplication"))));
        var count = await rows.LongCountAsync(ct); var offset = ((long)query.Page - 1) * query.PageSize;
        var items = offset >= count ? [] : await rows.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip((int)offset).Take(query.PageSize).ToListAsync(ct);
        await audit.ReadAsync("Member", memberId, ct);
        return new(items.Select(History).ToArray(), query.Page, query.PageSize, count);
    }
    private static MemberHistoryEntry History(AuditLog row)
    {
        using var metadata = row.MetadataJson is null ? null : JsonDocument.Parse(row.MetadataJson);
        var changes = metadata?.RootElement.TryGetProperty("changes", out var value) == true ? value : default;
        string? Read(string name) => changes.ValueKind == JsonValueKind.Object && changes.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;
        Guid? Id(string name) => Guid.TryParse(Read(name), out var id) ? id : null;
        return new(row.Id, row.Action, row.ActorAdminUserId, row.CreatedAtUtc, Read("previousStatus"), Read("status"), Id("previousCategoryId"), Id("categoryId"));
    }
}
