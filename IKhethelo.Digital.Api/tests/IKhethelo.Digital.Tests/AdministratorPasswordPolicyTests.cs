using IKhethelo.Digital.Application;
using Xunit;

namespace IKhethelo.Digital.Tests;

public sealed class AdministratorPasswordPolicyTests
{
    [Theory]
    [InlineData("ab1!xyz")]
    [InlineData("abcdefgh!")]
    [InlineData("abcdefgh1")]
    [InlineData("abcdefgh1 ")]
    public void RejectsInvalidPasswords(string password) => Assert.Throws<RequestFailure>(() => AdministratorValidation.Password(password));

    [Theory]
    [InlineData("abcdef1!")]
    [InlineData("密碼密碼ab1!")]
    [InlineData("pass word1!")]
    public void AcceptsValidPasswords(string password) => AdministratorValidation.Password(password);
}
