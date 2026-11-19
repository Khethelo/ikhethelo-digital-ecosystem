using IKhethelo.Digital.Application;
using IKhethelo.Digital.Domain;
using IKhethelo.Digital.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IKhethelo.Digital.Tests;

public sealed class NtmValidationTests
{
    [Theory]
    [InlineData("MK-001", null)]
    [InlineData(null, "DMV-001")]
    [InlineData("MK-001", "DMV-001")]
    public void EitherRegistrationIdentifierIsSufficient(string? mkmva, string? dmv) => NtmValidation.Veteran(new("Veteran", mkmva, dmv));
    [Theory]
    [InlineData(null, null)]
    [InlineData("  ", "\t")]
    public void MissingBothRegistrationIdentifiersIsRejected(string? mkmva, string? dmv) =>
        Assert.Equal(422, Assert.Throws<RequestFailure>(() => NtmValidation.Veteran(new("Veteran", mkmva, dmv))).Status);
    [Fact]
    public void InvalidStatusAndNegativeValuesAreRejectedTogether()
    {
        var failure = Assert.Throws<RequestFailure>(() => NtmValidation.Business(new(Guid.NewGuid(), "Business",
            OperationalStatus: "Invented", NumberOfEmployees: -1, YearsInOperation: -1, FundingAmount: -1)));
        Assert.Equal(new[] { "fundingAmount", "numberOfEmployees", "operationalStatus", "yearsInOperation" }, failure.Errors!.Keys.Order().ToArray());
    }
    [Fact]
    public void UnknownValuesRemainNullableAndNeedsRemainText() => NtmValidation.Business(new(Guid.NewGuid(), "Business",
        CurrentBusinessNeeds: "Equipment: lathe; marketing: website", PriorityNeed: "Training description"));
    [Fact]
    public void WorkbookDescriptionsCanBeRetainedWithoutInventingTypedValues()
    {
        NtmValidation.Veteran(new("Veteran", "MK-VERIFIED", EmailAddress: "Recorded source contact", MembershipReference: "DMV Database"));
        NtmValidation.Business(new(Guid.NewGuid(), "Business"));
    }
    [Theory]
    [InlineData("1.25", "12.34")]
    [InlineData("100000", "12.34")]
    [InlineData("1", "12.345")]
    [InlineData("1", "10000000000000")]
    public void PrecisionAndRangeAreRejectedRatherThanSilentlyRounded(string years, string funding)
    {
        Assert.Throws<RequestFailure>(() => NtmValidation.Business(new(Guid.NewGuid(), "Business",
            YearsInOperation: decimal.Parse(years, System.Globalization.CultureInfo.InvariantCulture),
            FundingAmount: decimal.Parse(funding, System.Globalization.CultureInfo.InvariantCulture))));
    }
    [Fact]
    public void DashboardGroupOrderingTranslatesToPostgresBeforeRecordProjection()
    {
        using var db = new DigitalDbContext(new DbContextOptionsBuilder<DigitalDbContext>().UseNpgsql("Host=localhost;Database=model_only").Options);
        var tenantId = Guid.NewGuid();
        var query = db.NtmBusinesses.Where(x => x.TenantId == tenantId && x.Sector != null)
            .GroupBy(x => x.Sector!).OrderBy(g => g.Key).Select(g => new NtmCount(g.Key, g.Count()));
        Assert.Contains("GROUP BY", query.ToQueryString(), StringComparison.Ordinal);
        Assert.Contains("ORDER BY", query.ToQueryString(), StringComparison.Ordinal);
    }
    [Fact]
    public void NamesLengthsAndPaginationAreValidated()
    {
        Assert.Throws<RequestFailure>(() => NtmValidation.Veteran(new(" ", "MK")));
        Assert.Throws<RequestFailure>(() => NtmValidation.Veteran(new("Veteran", new string('a', 101))));
        Assert.Throws<RequestFailure>(() => NtmValidation.Query(new(int.MaxValue, 100)));
        Assert.Throws<RequestFailure>(() => NtmValidation.Query(new(PageSize: 101)));
    }
    [Fact]
    public void ModelEnforcesOptionalBusinessRequiredVeteranAndTenantSafeUniqueness()
    {
        using var db = new DigitalDbContext(new DbContextOptionsBuilder<DigitalDbContext>().UseNpgsql("Host=localhost;Database=model_only").Options);
        var business = db.Model.FindEntityType(typeof(NtmBusiness))!;
        var fk = business.GetForeignKeys().Single(x => x.PrincipalEntityType.ClrType == typeof(NtmVeteran));
        Assert.True(fk.IsRequired); Assert.True(fk.IsUnique); Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        Assert.Equal(new[] { "TenantId", "VeteranId" }, fk.Properties.Select(x => x.Name));
        Assert.Equal(new[] { "TenantId", "Id" }, fk.PrincipalKey.Properties.Select(x => x.Name));
        var veteran = db.Model.FindEntityType(typeof(NtmVeteran))!;
        Assert.False(veteran.FindNavigation("Business")!.ForeignKey.IsRequiredDependent);
        foreach (var number in new[] { "Mkmva", "Dmv" })
        {
            Assert.NotNull(veteran.FindProperty("Normalized" + number + "Number")!.GetComputedColumnSql());
            Assert.Contains(veteran.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "TenantId", "Normalized" + number + "Number" }));
        }
    }
}
