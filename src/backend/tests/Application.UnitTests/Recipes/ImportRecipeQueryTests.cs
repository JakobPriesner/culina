using Application.Abstractions;
using Application.Recipes.Import;
using Domain.Shared;

namespace Application.UnitTests.Recipes;

/// <summary>What the paste-a-link import tells the client a page makes.</summary>
public class ImportRecipeQueryTests
{
    [Theory]
    [InlineData("\"12 Muffins\"", 12, "pieces", "Muffins")]
    [InlineData("[\"24\", \"Makes 24 cookies\"]", 24, "pieces", "cookies")]
    [InlineData("\"1 Kuchen (26 cm)\"", 1, "pieces", "Kuchen (26 cm)")]
    [InlineData("\"4 Portionen\"", 4, "servings", null)]
    [InlineData("6", 6, "servings", null)]
    [InlineData("\"6-8 servings\"", 8, "servings", null)]
    public async Task Handle_ShouldSayWhatThePageMakes_AsPiecesOrServings(
        string yield, decimal amount, string kind, string? label)
    {
        var html = $$"""
            <html><head><script type="application/ld+json">
            { "@context": "https://schema.org", "@type": "Recipe", "name": "Cake",
              "recipeIngredient": ["200 g flour"], "recipeInstructions": "Bake.", "recipeYield": {{yield}} }
            </script></head></html>
            """;

        var result = await Handler(html).Handle(
            new ImportRecipeQuery("https://example.com/cake", Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        var response = result.Match(value => value, error => throw new InvalidOperationException(error.Code));
        Assert.Equal(amount, response.Servings);
        Assert.Equal(kind, response.YieldKind);
        Assert.Equal(label, response.YieldLabel);
    }

    [Fact]
    public async Task Handle_ShouldSayNothingOfTheYield_WhenThePageGivesNoNumber()
    {
        const string html = """
            <script type="application/ld+json">{ "@type": "Recipe", "name": "Cake", "recipeYield": "a dozen" }</script>
            """;

        var result = await Handler(html).Handle(
            new ImportRecipeQuery("https://example.com/cake", Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        var response = result.Match(value => value, error => throw new InvalidOperationException(error.Code));
        Assert.Null(response.Servings);
        Assert.Null(response.YieldKind);
        Assert.Null(response.YieldLabel);
    }

    private static ImportRecipeQueryHandler Handler(string html) => new(new PageFetcher(html));

    private sealed class PageFetcher(string html) : IWebPageFetcher
    {
        public Task<Result<WebPage>> FetchAsync(Uri url, CancellationToken cancellationToken) =>
            Task.FromResult(Result<WebPage>.Success(new WebPage(url, html)));
    }
}
