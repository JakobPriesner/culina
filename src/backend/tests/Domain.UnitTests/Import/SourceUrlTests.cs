using Domain.Import;

namespace Domain.UnitTests.Import;

/// <summary>The original's address, which becomes a link on every reading of a recipe.</summary>
public class SourceUrlTests
{
    [Theory]
    [InlineData("https://www.chefkoch.de/rezepte/123/beans.html", "https://www.chefkoch.de/rezepte/123/beans.html")]
    [InlineData("http://example.com/recipe?ref=share", "http://example.com/recipe?ref=share")]
    [InlineData("  https://example.com/beans  ", "https://example.com/beans")]
    [InlineData("HTTPS://Example.COM/Beans", "https://example.com/Beans")]
    [InlineData("https://tandoor.lan:8080/view/recipe/7", "https://tandoor.lan:8080/view/recipe/7")]
    public void From_ShouldKeep_AnHttpAddressWithAHost(string text, string expected)
    {
        var url = SourceUrl.From(text);

        Assert.Equal(expected, url?.Value);
    }

    [Theory]
    // Scripts, however the host is dressed up to be labelled.
    [InlineData("javascript:alert(1)")]
    [InlineData("javascript://chefkoch.de/%0aalert(1)")]
    [InlineData("JavaScript://chefkoch.de/%0aalert(1)")]
    // Schemes desktop apps register for themselves, and the disk.
    [InlineData("search-ms:query=recipes")]
    [InlineData("ms-officecmd:{}")]
    [InlineData("file:///etc/passwd")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    // Other ordinary schemes that are still not a recipe page.
    [InlineData("ftp://example.com/recipe")]
    [InlineData("mailto:chef@example.com")]
    // Not an absolute address at all.
    [InlineData("/recipes/7")]
    [InlineData("chefkoch.de/rezepte/123")]
    [InlineData("https://")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void From_ShouldRefuse_AnythingThatIsNotAnHttpAddressWithAHost(string? text)
    {
        Assert.Null(SourceUrl.From(text));
    }

    [Fact]
    public void From_ShouldRefuse_AnAddressLongerThanAnyRecipePage()
    {
        var text = "https://example.com/" + new string('a', SourceUrl.MaxLength);

        Assert.Null(SourceUrl.From(text));
    }
}
