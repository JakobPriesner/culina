using Domain.Import;
using Infrastructure.Import.Tandoor;

namespace IntegrationTests.Import;

/// <summary>The picture address is data from the other server, so only the connection's own origin is followed.</summary>
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
        var url = TandoorLibrary.OnTheSameServer(Source(), written);

        // A path is the usual case; a whole address is what an instance configured with one produces.
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
        var url = TandoorLibrary.OnTheSameServer(Source(), written);

        Assert.Null(url);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ARecipeWithNoPicture_ShouldAskForNothing(string written)
    {
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
