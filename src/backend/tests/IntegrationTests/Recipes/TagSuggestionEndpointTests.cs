using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Recipes;

/// <summary>
/// The tags a recipe is offered, as the editor asks for them.
/// </summary>
/// <remarks>
/// Which tags are chosen is proved in <c>TagSuggestionTests</c>; this proves the
/// parts only the running API has: the household's tags read by their saved
/// names, and a recipe of another kitchen being none of the caller's business.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class TagSuggestionEndpointTests(PostgresFixture postgres)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Suggestions_ShouldOfferTheHouseholdsOwnTag_ByItsSlug()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        await kitchen.SaveAsync("Spaghetti Bolognese", "de", 15, 45, [("Hackfleisch", "g")], ["italienisch"], "Anbraten.");
        var pizza = await kitchen.SaveAsync(
            "Pizza Margherita", "de", 20, 15, [("Mehl", "g"), ("Tomaten", "g")], [], "Backen.");

        // Act
        var response = await kitchen.Client.GetAsync($"/api/v1/recipes/{pizza}/tag-suggestions", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var offered = response.Json!.Value.GetProperty("items").EnumerateArray().ToList();
        var italian = Assert.Single(offered, one => one.GetProperty("name").GetString() == "italienisch");
        Assert.Equal("italienisch", italian.GetProperty("slug").GetString());
    }

    [Fact]
    public async Task Suggestions_ShouldSayNotFound_ForARecipeInAnotherHousehold()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipeId = await kitchen.SaveAsync("Pizza Margherita", "de", 20, 15, [("Mehl", "g")], [], "Backen.");
        using var stranger = await Kitchen.StrangerAsync(postgres);

        // Act
        var response = await stranger.GetAsync($"/api/v1/recipes/{recipeId}/tag-suggestions", Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("recipes.not_found", response.ProblemCode);
    }
}
