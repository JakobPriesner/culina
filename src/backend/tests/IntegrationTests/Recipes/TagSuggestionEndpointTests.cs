using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Recipes;

/// <summary>The API-level parts of tag suggestions; which tags are chosen is covered in <c>TagSuggestionTests</c>.</summary>
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
