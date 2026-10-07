using Domain.Users;
using TestSupport;

namespace Domain.UnitTests.Users;

public class EmailTests
{
    [Theory]
    [InlineData("ada@example.com")]
    [InlineData("ada.lovelace+recipes@example.co.uk")]
    [InlineData("a@b.co")]
    public void Create_ShouldSucceed_WhenTheAddressLooksLikeOne(string value)
    {
        var result = Email.Create(value);

        Assert.Equal(value, result.ShouldBeSuccess().Value);
    }

    [Theory]
    [InlineData("  Ada@Example.COM  ", "ada@example.com")]
    [InlineData("ADA@EXAMPLE.COM", "ada@example.com")]
    public void Create_ShouldNormalise_SoComparisonIsUnambiguous(string input, string expected)
    {
        var result = Email.Create(input);

        Assert.Equal(expected, result.ShouldBeSuccess().Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign")]
    [InlineData("@example.com")]
    [InlineData("ada@")]
    [InlineData("ada@example")]
    [InlineData("ada@@example.com")]
    [InlineData("ada lovelace@example.com")]
    public void Create_ShouldFail_WhenTheAddressIsObviouslyWrong(string? value)
    {
        var result = Email.Create(value);

        result.ShouldBeFailure(UserErrors.InvalidEmail);
    }

    [Fact]
    public void Create_ShouldFail_WhenTheAddressExceedsTheColumnLength()
    {
        var tooLong = new string('a', Email.MaxLength) + "@example.com";

        var result = Email.Create(tooLong);

        result.ShouldBeFailure(UserErrors.InvalidEmail);
    }

    [Fact]
    public void Domain_ShouldReturnOnlyTheHost_SoALogLineCarriesNoPersonalData()
    {
        var email = Email.Create("ada@example.com").ShouldBeSuccess();

        var domain = email.Domain;

        Assert.Equal("example.com", domain);
    }
}
