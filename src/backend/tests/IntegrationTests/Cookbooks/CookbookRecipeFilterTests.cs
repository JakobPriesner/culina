using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Cookbooks;

/// <summary>Reading a cookbook through the recipe list: a cookbook is a view of the collection, so a shelf gets search, tags and paging for one filter clause.</summary>
[Collection(RequiresDatabase.Name)]
public class CookbookRecipeFilterTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Filter_ShouldReturnOnlyWhatIsOnTheShelf()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var cookbookId = await CookbookAsync(client, householdId, "Sonntags");

        var onIt = await RecipeAsync(client, householdId, "Braten");
        await RecipeAsync(client, householdId, "Nicht drauf");

        await client.PutAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{onIt}", new { }, Token);

        var response = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}",
            Token);

        var titles = Titles(response);

        Assert.Equal(["Braten"], titles);
        Assert.Equal(1, response.Json!.Value.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Filter_ShouldReadInTheOrderTheShelfWasBuilt()
    {
        // Oldest first, like a table of contents: the order it was built in is the order meant.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var cookbookId = await CookbookAsync(client, householdId, "Der Reihe nach");

        foreach (var title in new[] { "Zuerst", "Dann", "Zuletzt" })
        {
            var recipeId = await RecipeAsync(client, householdId, title);

            await client.PutAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}", new { }, Token);
        }

        var response = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}",
            Token);

        Assert.Equal(["Zuerst", "Dann", "Zuletzt"], Titles(response));
    }

    [Fact]
    public async Task Filter_ShouldStillHonourTheSearchBox()
    {
        // The argument for reusing GET /recipes: searching a cookbook needed no code of its own.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var cookbookId = await CookbookAsync(client, householdId, "Alles");

        foreach (var title in new[] { "Kartoffelsuppe", "Linsensuppe", "Apfelkuchen" })
        {
            var recipeId = await RecipeAsync(client, householdId, title);

            await client.PutAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}", new { }, Token);
        }

        var response = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}&query=suppe",
            Token);

        Assert.Equal(["Kartoffelsuppe", "Linsensuppe"], [.. Titles(response).Order()]);
    }

    [Fact]
    public async Task Filter_ShouldStillHonourAnExplicitSort()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var cookbookId = await CookbookAsync(client, householdId, "Alphabetisch");

        foreach (var title in new[] { "Zwiebelkuchen", "Apfelkuchen" })
        {
            var recipeId = await RecipeAsync(client, householdId, title);

            await client.PutAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}", new { }, Token);
        }

        var response = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={cookbookId}&sort=title",
            Token);

        Assert.Equal(["Apfelkuchen", "Zwiebelkuchen"], Titles(response));
    }

    [Fact]
    public async Task Filter_ShouldPageWithoutRepeatingOrLosingARecipe()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var cookbookId = await CookbookAsync(client, householdId, "Viele");

        var expected = new[] { "Eins", "Zwei", "Drei", "Vier", "Fünf" };

        foreach (var title in expected)
        {
            var recipeId = await RecipeAsync(client, householdId, title);

            await client.PutAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}", new { }, Token);
        }

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

        Assert.Equal(expected, seen);
    }

    [Fact]
    public async Task Filter_ShouldBeAnEmptyPageForACookbookThatIsNotYours()
    {
        // Deliberately not a 404: `cookbookId` is a filter value, like an unknown tag slug. The cookbook's own header read answers 404.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        await RecipeAsync(client, householdId, "Meins");

        var response = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId={Guid.NewGuid()}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(response.Json!.Value.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Sort_ShouldRefuseCookbookOrderWithoutACookbook()
    {
        // A silently ignored filter returns wrong data that looks right, which the query-parameter guard exists to stop.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var response = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&sort=cookbookOrder",
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Filter_ShouldRejectSomethingThatIsNotAnId()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var response = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&cookbookId=irgendwas",
            Token);

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
