using Xunit;

namespace IKhethelo.Digital.Tests;

public sealed class LocalTestPostgresTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public void AcceptsNumericLoopback(string host) =>
        Assert.NotNull(LocalTestPostgres.RequireLoopback($"Host={host};Database=postgres;Username=test"));

    [Theory]
    [InlineData("example.com")]
    [InlineData("192.0.2.1")]
    [InlineData("127.0.0.1,192.0.2.1")]
    public void RejectsNonLoopbackHosts(string host) =>
        Assert.Throws<InvalidOperationException>(() => LocalTestPostgres.RequireLoopback($"Host={host};Database=postgres;Username=test"));
}
