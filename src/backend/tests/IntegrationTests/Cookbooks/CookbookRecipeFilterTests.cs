using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Cookbooks;

/// <summary>
/// Reading a cookbook through the recipe list.
/// </summary>
/// <remarks>
/// The bet this feature is built on: a cookbook is a view of the collection,
/// not a second collection. If that holds, a shelf gets the search, the tags,
/// the time ceiling and the paging for the price of one filter clause — and
/// these are the tests that say whether it holds.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class CookbookRecipeFilterTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Filter_ShouldReturnOnlyWhatIsOnTheShelf()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var cookbookId = await CookbookAsync(client, householdId, "Sonntags");

        var onIt = await RecipeAsync(client, householdId, "Braten");
        await RecipeAsync(client, householdId, "Nicht drauf");

        await client.PutAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{onIt}", new { }, Token);

        // Act
        var response = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}",
            Token);

        // Assert
        var titles = Titles(response);

        Assert.Equal(["Braten"], titles);
        Assert.Equal(1, response.Json!.Value.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Filter_ShouldReadInTheOrderTheShelfWasBuilt()
    {
        // Arrange
        // Oldest first, like a table of contents. The order somebody built it
        // in is the order they meant.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var cookbookId = await CookbookAsync(client, householdId, "Der Reihe nach");

        foreach (var title in new[] { "Zuerst", "Dann", "Zuletzt" })
        {
            var recipeId = await RecipeAsync(client, householdId, title);

            await client.PutAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}", new { }, Token);
        }

        // Act
        var response = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}",
            Token);

        // Assert
        Assert.Equal(["Zuerst", "Dann", "Zuletzt"], Titles(response));
    }

    [Fact]
    public async Task Filter_ShouldStillHonourTheSearchBox()
    {
        // Arrange
        // This is the whole argument for reusing GET /recipes: searching inside
        // a cookbook needed no code of its own.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var cookbookId = await CookbookAsync(client, householdId, "Alles");

        foreach (var title in new[] { "Kartoffelsuppe", "Linsensuppe", "Apfelkuchen" })
        {
            var recipeId = await RecipeAsync(client, householdId, title);

            await client.PutAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}", new { }, Token);
        }

        // Act
        var response = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}&query=suppe",
            Token);

        // Assert
        Assert.Equal(["Kartoffelsuppe", "Linsensuppe"], [.. Titles(response).Order()]);
    }

    [Fact]
    public async Task Filter_ShouldStillHonourAnExplicitSort()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var cookbookId = await CookbookAsync(client, householdId, "Alphabetisch");

        foreach (var title in new[] { "Zwiebelkuchen", "Apfelkuchen" })
        {
            var recipeId = await RecipeAsync(client, householdId, title);

            await client.PutAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}", new { }, Token);
        }

        // Act
        var response = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}&sort=title",
            Token);

        // Assert
        Assert.Equal(["Apfelkuchen", "Zwiebelkuchen"], Titles(response));
    }

    [Fact]
    public async Task Filter_ShouldPageWithoutRepeatingOrLosingARecipe()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var cookbookId = await CookbookAsync(client, householdId, "Viele");

        var expected = new[] { "Eins", "Zwei", "Drei", "Vier", "Fünf" };

        foreach (var title in expected)
        {
            var recipeId = await RecipeAsync(client, householdId, title);

            await client.PutAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}", new { }, Token);
        }

        // Act
        List<string> seen = [];
        string? cursor = null;

        do
        {
            var page = await client.GetAsync(
                $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}&limit=2"
                + (cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}"),
                Token);

            seen.AddRange(Titles(page));
            cursor = page.Json!.Value.TryGetProperty("nextCursor", out var next) && next.ValueKind
                is System.Text.Json.JsonValueKind.String
                ? next.GetString()
                : null;
        }
        while (cursor is not null);

        // Assert
        Assert.Equal(expected, seen);
    }

    [Fact]
    public async Task Filter_ShouldBeAnEmptyPageForACookbookThatIsNotYours()
    {
        // Arrange
        // Deliberately not a 404. `cookbookId` is a filter value, not a
        // resource named in the path, and an unknown tag slug already behaves
        // exactly this way. The cookbook's own page reads GET /cookbooks/{id}
        // for its header, and that does answer 404.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        await RecipeAsync(client, householdId, "Meins");

        // Act
        var response = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={Guid.NewGuid()}",
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.Json!.Value.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Sort_ShouldRefuseCookbookOrderWithoutACookbook()
    {
        // Arrange
        // A filter that silently does nothing returns wrong data that looks
        // right, which is the failure the query-parameter guard exists for.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        // Act
        var response = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&sort=cookbookOrder",
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Filter_ShouldRejectSomethingThatIsNotAnId()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        // Act
        var response = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId=irgendwas",
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static List<string> Titles(ApiResponse response) =>
        [
            .. response.Json!.Value.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("title").GetString()!)
        ];

    private static async Task<Guid> CookbookAsync(ApiClient client, Guid householdId, string name)
    {
        var created = await client.PostAsync("/api/v1/cookbooks", new { householdId, name }, Token);

        return created.Json!.Value.GetProperty("cookbookId").GetGuid();
    }

    private static async Task<Guid> RecipeAsync(ApiClient client, Guid householdId, string title)
    {
        var created = await client.PostAsync("/api/v1/recipes", new { householdId, title }, Token);

        return created.Json!.Value.GetProperty("recipeId").GetGuid();
    }

    private static async Task<Guid> HouseholdAsync(ApiClient client)
    {
        var me = await client.GetAsync("/api/v1/users/me", Token);

        return me.Json!.Value.GetProperty("households")[0].GetProperty("householdId").GetGuid();
    }

    private async Task<ApiClient> SignedInAsync()
    {
        await postgres.ResetAsync(Token);

        var client = postgres.Api.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email = "ada@example.com", displayName = "Ada", password = Password },
            Token);
        await client.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password },
            Token);

        return client;
    }
}
