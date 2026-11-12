using IKhethelo.Digital.Application;
using Xunit;

namespace IKhethelo.Digital.Tests;

public sealed class ValidationTests
{
    [Theory]
    [InlineData(2008, 11, 5, true)]
    [InlineData(2008, 11, 6, false)]
    [InlineData(2009, 1, 1, false)]
    [InlineData(2027, 1, 1, false)]
    public void AdultBoundaryUsesCalendarAnniversary(int year, int month, int day, bool expected) =>
        Assert.Equal(expected, Validation.IsAdult(new(year, month, day), new(2026, 11, 5)));

    [Fact]
    public void LeapAnniversaryUsesFebruary28InNonLeapYear()
    {
        Assert.False(Validation.IsAdult(new(2008, 2, 29), new(2026, 2, 27)));
        Assert.True(Validation.IsAdult(new(2008, 2, 29), new(2026, 2, 28)));
    }
    [Fact]
    public void SubmissionDateUsesJohannesburgRatherThanUtc() =>
        Assert.Equal(new DateOnly(2026, 11, 6), Validation.LocalDate(new FixedClock(new DateTimeOffset(2026, 11, 5, 22, 0, 0, TimeSpan.Zero))));

    [Theory]
    [InlineData("+27821234567\n")]
    [InlineData("082 123 4567")]
    [InlineData("+0123456789")]
    public void PhoneRequiresCanonicalFormat(string phone)
    {
        var failure = Assert.Throws<RequestFailure>(() => Validation.Submission(Valid() with { PhoneNumber = phone }, new(2026, 11, 5), true));
        Assert.Equal(422, failure.Status); Assert.Contains("phoneNumber", failure.Errors!.Keys);
    }
    [Fact]
    public void UnicodeNameLimitsMatchPostgresCodePoints()
    {
        Validation.Submission(Valid() with { FirstName = string.Concat(Enumerable.Repeat("\U0001F600", 100)) }, new(2026, 11, 5), true);
        Assert.Throws<RequestFailure>(() => Validation.Submission(Valid() with { FirstName = "a" + new string('\u0301', 100) }, new(2026, 11, 5), true));
    }
    [Fact]
    public void RequiredConsentsAndFieldsAreAuthoritative()
    {
        var failure = Assert.Throws<RequestFailure>(() => Validation.Submission(new(null, "A", null, null, "invalid", false, false), new(2026, 11, 5), true));
        Assert.Equal(7, failure.Errors!.Count);
    }
    [Fact]
    public void EmailNormalizationIsStable() => Assert.Equal("MEMBER@EXAMPLE.COM", Validation.NormalizeEmail(" member@example.com "));
    private static ApplicationSubmission Valid() => new("First", "Last", new(1990, 1, 1), "+27821234567", "member@example.com", true, true);
}

public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
