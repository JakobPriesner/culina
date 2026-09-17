using Domain.Import;
using Infrastructure.Import.Tandoor;

namespace IntegrationTests.Import;

/// <summary>
/// Which picture addresses the server will follow.
/// </summary>
/// <remarks>
/// The address of a recipe's photo arrives inside a response, which makes it
/// data from the other server rather than anything a person typed. Following it
/// wherever it points would hand a compromised — or merely odd — instance the
/// ability to name the address this server connects to, which is the one thing
/// the whole import is careful about. So the rule is: the connection's own
/// origin, and nowhere else.
/// </remarks>
public class TandoorPictureAddressTests
{
    [Theory]
    [InlineData("/media/recipes/pie.jpg", "https://recipes.example.com/media/recipes/pie.jpg")]
    [InlineData("media/recipes/pie.jpg", "https://recipes.example.com/media/recipes/pie.jpg")]
    [InlineData(
        "https://recipes.example.com/media/pie.jpg",
        "https://recipes.example.com/media/pie.jpg")]
    public void APictureOnTheSameServer_ShouldBeFollowed(string written, string expected)
    {
        // Act
        var url = TandoorLibrary.OnTheSameServer(Source(), written);

        // Assert
        // A path is the usual case — Tandoor's own media directory — and a
        // whole address is what an instance configured with one produces.
        Assert.Equal(expected, url?.ToString());
    }

    [Theory]
    // Somewhere else entirely.
    [InlineData("https://evil.example.net/pie.jpg")]
    // The cloud metadata service, which is the reason this rule exists.
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    // The same host, a different port: a different server.
    [InlineData("https://recipes.example.com:9000/pie.jpg")]
    // The same host, no longer over TLS.
    [InlineData("http://recipes.example.com/pie.jpg")]
    // Protocol-relative, which resolves to another host rather than a path.
    [InlineData("//evil.example.net/pie.jpg")]
    [InlineData("file:///etc/passwd")]
    public void APictureAnywhereElse_ShouldNotBe(string written)
    {
        // Act
        var url = TandoorLibrary.OnTheSameServer(Source(), written);

        // Assert
        Assert.Null(url);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ARecipeWithNoPicture_ShouldAskForNothing(string written)
    {
        // Act & Assert
        Assert.Null(TandoorLibrary.OnTheSameServer(Source(), written));
    }

    private static RecipeSource Source()
    {
        var address = SourceAddress.Create("https://recipes.example.com")
            .Match(value => value, error => throw new InvalidOperationException(error.Code));

        return RecipeSource.Restore(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SourceKind.Tandoor,
            "recipes.example.com",
            address,
            "tda_secret",
            Guid.NewGuid(),
            DateTimeOffset.UnixEpoch,
            lastUsedAt: null,
            version: 1);
    }
}
