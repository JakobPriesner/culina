using System.Net;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Ingredients;

[Collection(RequiresDatabase.Name)]
public class IngredientMergeEndpointTests(PostgresFixture postgres)
{
    private const string Butter = "Q611000";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Post_ShouldRewriteTheHouseholdsRecipesAndMoveTheirVersion()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var curry = await SaveAsync(kitchen, "Curry", "Hühnchenbrustfilet", "Reis");
        await SaveAsync(kitchen, "Salat", "Hähnchenbrust");
        var before = await kitchen.Client.GetAsync(RecipePath(curry), Token);

        // Act
        var merged = await MergeAsync(kitchen, "Hühnchenbrustfilet", "Hähnchenbrust");
        var after = await kitchen.Client.GetAsync(RecipePath(curry), Token);

        // Assert
        Assert.Equal(HttpStatusCode.Created, merged.StatusCode);
        Assert.Equal(1, merged.Json!.Value.GetProperty("recipeCount").GetInt32());
        Assert.NotEqual(before.ETag, after.ETag);
        Assert.Equal(["Hähnchenbrust", "Reis"], Names(after));
    }

    [Fact]
    public async Task Post_ShouldRewriteEverySpellingThatFoldsTheSame()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var pesto = await SaveAsync(kitchen, "Pesto", "olivenöl");
        var salad = await SaveAsync(kitchen, "Salat", "Olivenoel");

        // Act
        var merged = await MergeAsync(kitchen, "Olivenöl", "Rapsöl");

        // Assert
        Assert.Equal(2, merged.Json!.Value.GetProperty("recipeCount").GetInt32());
        Assert.Equal(["Rapsöl"], Names(await kitchen.Client.GetAsync(RecipePath(pesto), Token)));
        Assert.Equal(["Rapsöl"], Names(await kitchen.Client.GetAsync(RecipePath(salad), Token)));
    }

    [Fact]
    public async Task Put_ShouldStoreTheKeptName_WhenALaterSaveUsesAMergedOne()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        await MergeAsync(kitchen, "Hühnchenbrustfilet", "Hähnchenbrust");

        // Act: an edit, as an import would, writes the old name again.
        var recipe = await SaveAsync(kitchen, "Curry", "Hühnchenbrustfilet");
        var read = await kitchen.Client.GetAsync(RecipePath(recipe), Token);

        // Assert
        Assert.Equal(["Hähnchenbrust"], Names(read));
    }

    [Fact]
    public async Task Post_ShouldMoveTheFoodCorrectionToTheKeptName()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        await SaveAsync(kitchen, "Brot", "Margarine");
        await kitchen.Client.PutAsync(
            $"/api/v1/households/{kitchen.HouseholdId}/ingredients/Margarine",
            new { food = Butter },
            Token);

        // Act
        await MergeAsync(kitchen, "Margarine", "Streichfett");
        var facts = await kitchen.Client.GetAsync(
            $"/api/v1/households/{kitchen.HouseholdId}/nutrition/ingredients",
            Token);

        // Assert
        var fact = Assert.Single(facts.Json!.Value.GetProperty("items").EnumerateArray());
        Assert.Equal("streichfett", fact.GetProperty("name").GetString());
        Assert.Equal(Butter, fact.GetProperty("food").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Post_ShouldKeepMergesOneStepDeep()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        await MergeAsync(kitchen, "Hühnerbrust", "Hühnchenbrust");

        // Act: the kept name goes away too, then something is merged into a merged name.
        await MergeAsync(kitchen, "Hühnchenbrust", "Hähnchenbrust");
        var followed = await MergeAsync(kitchen, "Poulet", "Hühnerbrust");
        var merges = await kitchen.Client.GetAsync(MergesPath(kitchen), Token);

        // Assert
        Assert.Equal("Hähnchenbrust", followed.Json!.Value.GetProperty("to").GetString());
        Assert.Equal(
            [("Hühnchenbrust", "Hähnchenbrust"), ("Hühnerbrust", "Hähnchenbrust"), ("Poulet", "Hähnchenbrust")],
            Items(merges).Select(item => (item.GetProperty("from").GetString(), item.GetProperty("to").GetString())));
    }

    [Fact]
    public async Task Post_ShouldRefuse_WhenANameIsMergedIntoItself()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var merged = await MergeAsync(kitchen, "Salz", "Salz");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, merged.StatusCode);
        Assert.Equal("ingredients.same_name", merged.ProblemCode);
    }

    [Fact]
    public async Task Delete_ShouldStopApplyingTheMergeButLeaveRewrittenRecipes()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var old = await SaveAsync(kitchen, "Curry", "Hühnchenbrustfilet");
        await MergeAsync(kitchen, "Hühnchenbrustfilet", "Hähnchenbrust");

        // Act
        var deleted = await kitchen.Client.DeleteAsync($"{MergesPath(kitchen)}/H%C3%BChnchenbrustfilet", Token);
        var again = await kitchen.Client.DeleteAsync($"{MergesPath(kitchen)}/H%C3%BChnchenbrustfilet", Token);
        var fresh = await SaveAsync(kitchen, "Wok", "Hühnchenbrustfilet");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Empty(Items(await kitchen.Client.GetAsync(MergesPath(kitchen), Token)));
        Assert.Equal(["Hähnchenbrust"], Names(await kitchen.Client.GetAsync(RecipePath(old), Token)));
        Assert.Equal(["Hühnchenbrustfilet"], Names(await kitchen.Client.GetAsync(RecipePath(fresh), Token)));
    }

    [Fact]
    public async Task GetNames_ShouldCountRecipesPerName_MostUsedFirst()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        await SaveAsync(kitchen, "Brot", "Mehl", "Salz");
        await SaveAsync(kitchen, "Suppe", "Salz");

        // Act
        var names = await kitchen.Client.GetAsync(
            $"/api/v1/households/{kitchen.HouseholdId}/ingredient-names",
            Token);

        // Assert
        Assert.Equal(
            [("Salz", 2), ("Mehl", 1)],
            Items(names).Select(item => (item.GetProperty("name").GetString(), item.GetProperty("recipeCount").GetInt32())));
    }

    [Fact]
    public async Task GetSuggestions_ShouldProposeTheMostUsedLookalike_UntilDismissed()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        await SaveAsync(kitchen, "Curry", "Hühnchenbrustfilet");
        await SaveAsync(kitchen, "Salat", "Hähnchenbrust");
        await SaveAsync(kitchen, "Wrap", "Hähnchenbrust");

        // Act
        var suggested = await kitchen.Client.GetAsync(SuggestionsPath(kitchen), Token);
        var dismissed = await kitchen.Client.DeleteAsync(
            $"{SuggestionsPath(kitchen)}/{Uri.EscapeDataString("Hühnchenbrustfilet")}",
            Token);
        var after = await kitchen.Client.GetAsync(SuggestionsPath(kitchen), Token);

        // Assert
        var suggestion = Assert.Single(Items(suggested));
        Assert.Equal("Hühnchenbrustfilet", suggestion.GetProperty("from").GetProperty("name").GetString());
        Assert.Equal("Hähnchenbrust", suggestion.GetProperty("to").GetProperty("name").GetString());
        Assert.Equal(2, suggestion.GetProperty("to").GetProperty("recipeCount").GetInt32());
        Assert.Equal("same_food", suggestion.GetProperty("reason").GetString());
        Assert.Equal(HttpStatusCode.NoContent, dismissed.StatusCode);
        Assert.Empty(Items(after));
    }

    [Theory]
    [InlineData("Hühnchenbrustfilet")]
    [InlineData("Haehnchenbrust")]
    public async Task GetIngredients_ShouldPutTheHouseholdsOwnNameFirst_WhenTheTypedOneMeansIt(string typed)
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        await SaveAsync(kitchen, "Salat", "Hähnchenbrust");

        // Act
        var suggested = await kitchen.Client.GetAsync(IngredientsPath(kitchen, typed), Token);

        // Assert
        var first = Items(suggested)[0];
        Assert.Equal("Hähnchenbrust", first.GetProperty("name").GetString());
        Assert.True(first.GetProperty("equivalent").GetBoolean());
    }

    [Fact]
    public async Task GetIngredients_ShouldOfferTheKeptName_ForAMergedOne()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        await MergeAsync(kitchen, "Poulet", "Hähnchenbrust");

        // Act
        var suggested = await kitchen.Client.GetAsync(IngredientsPath(kitchen, "poulet"), Token);

        // Assert
        var first = Items(suggested)[0];
        Assert.Equal("Hähnchenbrust", first.GetProperty("name").GetString());
        Assert.True(first.GetProperty("equivalent").GetBoolean());
    }

    [Fact]
    public async Task GetIngredients_ShouldMarkNothing_WhenTheTypedNameIsAlreadyInUse()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        await SaveAsync(kitchen, "Salat", "Hähnchenbrust");
        await SaveAsync(kitchen, "Curry", "Hähnchenbrustfilet");

        // Act
        var suggested = await kitchen.Client.GetAsync(IngredientsPath(kitchen, "Hähnchenbrustfilet"), Token);

        // Assert
        Assert.DoesNotContain(Items(suggested), item => item.GetProperty("equivalent").GetBoolean());
    }

    [Fact]
    public async Task Everything_ShouldAnswerNotFound_ForAnotherHousehold()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var stranger = await Kitchen.StrangerAsync(postgres);

        // Act
        var list = await stranger.GetAsync(MergesPath(kitchen), Token);
        var merge = await stranger.PostAsync(MergesPath(kitchen), new { from = "Salz", to = "Meersalz" }, Token);
        var suggestions = await stranger.GetAsync(SuggestionsPath(kitchen), Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, merge.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, suggestions.StatusCode);
    }

    private static string RecipePath(Guid recipeId) => $"/api/v1/recipes/{recipeId}";

    private static string MergesPath(Kitchen kitchen) =>
        $"/api/v1/households/{kitchen.HouseholdId}/ingredient-merges";

    private static string SuggestionsPath(Kitchen kitchen) =>
        $"/api/v1/households/{kitchen.HouseholdId}/ingredient-merge-suggestions";

    private static string IngredientsPath(Kitchen kitchen, string typed) =>
        $"/api/v1/households/{kitchen.HouseholdId}/ingredients?q={Uri.EscapeDataString(typed)}&language=de";

    private static Task<ApiResponse> MergeAsync(Kitchen kitchen, string from, string to) =>
        kitchen.Client.PostAsync(MergesPath(kitchen), new { from, to }, Token);

    private static Task<Guid> SaveAsync(Kitchen kitchen, string title, params string[] names) =>
        kitchen.SaveAsync(title, "de", null, null, [.. names.Select(name => (name, (string?)null))], [], "Kochen.");

    private static List<JsonElement> Items(ApiResponse response) =>
        [.. response.Json!.Value.GetProperty("items").EnumerateArray()];

    private static List<string> Names(ApiResponse recipe) =>
        [.. recipe.Json!.Value.GetProperty("groups")[0].GetProperty("ingredients").EnumerateArray()
            .Select(line => line.GetProperty("name").GetString()!)];
}
