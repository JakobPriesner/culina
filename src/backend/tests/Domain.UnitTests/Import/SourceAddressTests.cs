using Domain.Import;

namespace Domain.UnitTests.Import;

/// <summary>What somebody pastes, and what gets stored.</summary>
/// <remarks>
/// Every case is something a real person types into the box (an address bar's contents, a bare
/// host, a trailing slash); the rule is one instance, one address, so connecting twice is a
/// conflict the database can see.
/// </remarks>
public class SourceAddressTests
{
    [Theory]
    [InlineData("https://recipes.example.com")]
    [InlineData("https://recipes.example.com/")]
    [InlineData("https://recipes.example.com/search/?page=3")]
    [InlineData("https://recipes.example.com/api")]
    [InlineData("  https://recipes.example.com/  ")]
    public void Create_ShouldReduceEveryFormOfOneInstance_ToTheSameAddress(string typed)
    {
        var address = SourceAddress.Create(typed);

        // Without this, "the address bar" and "the address" are two connections
        // to one server, each with its own token.
        Assert.Equal(
            "https://recipes.example.com",
            address.Match(value => value.Value, error => error.Code));
    }

    [Fact]
    public void Create_ShouldAssumeHttps_WhenNoSchemeWasTyped()
    {
        var address = SourceAddress.Create("recipes.example.com");

        // A bare host is the common paste, and the token in every later request
        // is a credential — so the secure guess is the only guess.
        Assert.Equal(
            "https://recipes.example.com",
            address.Match(value => value.Value, error => error.Code));
    }

    [Fact]
    public void Create_ShouldKeepAPort_BecauseSelfHostedAppsLiveOnOne()
    {
        var address = SourceAddress.Create("http://tandoor.lan:8080");

        Assert.Equal(
            "http://tandoor.lan:8080",
            address.Match(value => value.Value, error => error.Code));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://recipes.example.com")]
    [InlineData("javascript:alert(1)")]
    public void Create_ShouldRefuse_WhatIsNotAnHttpAddress(string? typed)
    {
        var address = SourceAddress.Create(typed);

        // Parsing proves only that it is an address: file:///etc/passwd parses
        // perfectly.
        Assert.Equal(
            ImportErrors.InvalidSourceAddress.Code,
            address.Match(value => value.Value, error => error.Code));
    }

    [Fact]
    public void Create_ShouldRefuse_AnAddressLongerThanAnyoneTypes()
    {
        var address = SourceAddress.Create($"https://{new string('a', SourceAddress.MaxLength)}.com");

        Assert.Equal(
            ImportErrors.InvalidSourceAddress.Code,
            address.Match(value => value.Value, error => error.Code));
    }

    [Fact]
    public void At_ShouldBuildOnTheAuthority_AndNotOnWhateverPathWasPasted()
    {
        var address = SourceAddress.Create("https://recipes.example.com/api/")
            .Match(value => value, error => throw new InvalidOperationException(error.Code));

        var url = address.At("/api/recipe/?page=1");

        // The first person to paste a path would otherwise get /api/api/recipe/
        // on every request this connection ever makes.
        Assert.Equal("https://recipes.example.com/api/recipe/?page=1", url.ToString());
    }
}
