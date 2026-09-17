using Domain.Import;

namespace Domain.UnitTests.Import;

/// <summary>
/// What somebody pastes, and what gets stored.
/// </summary>
/// <remarks>
/// Every case here is something a real person actually types into the box: the
/// contents of a browser's address bar, a bare host name, a trailing slash. The
/// rule they are all serving is that one instance has one address, so
/// connecting to it twice is a conflict the database can see.
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
        // Act
        var address = SourceAddress.Create(typed);

        // Assert
        // Without this, "the address bar" and "the address" are two connections
        // to one server, each with its own token.
        Assert.Equal(
            "https://recipes.example.com",
            address.Match(value => value.Value, error => error.Code));
    }

    [Fact]
    public void Create_ShouldAssumeHttps_WhenNoSchemeWasTyped()
    {
        // Act
        var address = SourceAddress.Create("recipes.example.com");

        // Assert
        // A bare host is the common paste, and the token in every later request
        // is a credential — so the secure guess is the only guess.
        Assert.Equal(
            "https://recipes.example.com",
            address.Match(value => value.Value, error => error.Code));
    }

    [Fact]
    public void Create_ShouldKeepAPort_BecauseSelfHostedAppsLiveOnOne()
    {
        // Act
        var address = SourceAddress.Create("http://tandoor.lan:8080");

        // Assert
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
        // Act
        var address = SourceAddress.Create(typed);

        // Assert
        // Parsing proves only that it is an address: file:///etc/passwd parses
        // perfectly.
        Assert.Equal(
            ImportErrors.InvalidSourceAddress.Code,
            address.Match(value => value.Value, error => error.Code));
    }

    [Fact]
    public void Create_ShouldRefuse_AnAddressLongerThanAnyoneTypes()
    {
        // Act
        var address = SourceAddress.Create($"https://{new string('a', SourceAddress.MaxLength)}.com");

        // Assert
        Assert.Equal(
            ImportErrors.InvalidSourceAddress.Code,
            address.Match(value => value.Value, error => error.Code));
    }

    [Fact]
    public void At_ShouldBuildOnTheAuthority_AndNotOnWhateverPathWasPasted()
    {
        // Arrange
        var address = SourceAddress.Create("https://recipes.example.com/api/")
            .Match(value => value, error => throw new InvalidOperationException(error.Code));

        // Act
        var url = address.At("/api/recipe/?page=1");

        // Assert
        // The first person to paste a path would otherwise get /api/api/recipe/
        // on every request this connection ever makes.
        Assert.Equal("https://recipes.example.com/api/recipe/?page=1", url.ToString());
    }
}
