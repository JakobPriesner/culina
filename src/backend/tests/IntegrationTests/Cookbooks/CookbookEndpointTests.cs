using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Cookbooks;

/// <summary>
/// What a cookbook is, proved against a real database.
/// </summary>
/// <remarks>
/// The rules worth a test are the ones a reader would otherwise have to take on
/// trust: that a shelf points at recipes rather than owning them, that adding
/// the same recipe twice is genuinely nothing, and that a shelf belonging to
/// another kitchen is indistinguishable from one that never existed.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class CookbookEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_ShouldNeedOnlyAName()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        // Act
        var response = await client.PostAsync(
            "/api/v1/cookbooks",
            new { householdId, name = "Weihnachten" },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Weihnachten", response.Json!.Value.GetProperty("name").GetString());
        Assert.Equal(0, response.Json!.Value.GetProperty("recipeCount").GetInt32());
    }

    [Fact]
    public async Task Get_ShouldNotExistForSomebodyElsesHousehold()
    {
        // Arrange
        using var client = await SignedInAsync();
        var cookbookId = await CookbookAsync(client, await HouseholdAsync(client), "Sonntagsbraten");

        using var stranger = await SecondAccountAsync();

        // Act
        var response = await stranger.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token);

        // Assert
        // 404, not 403: a stranger learns nothing about which shelves exist.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AddRecipe_ShouldBeNothingTheSecondTime()
    {
        // Arrange
        // A double tap and a retried request are both ordinary on a phone, and
        // neither means "put it on twice".
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var cookbookId = await CookbookAsync(client, householdId, "Wochentags");
        var recipeId = await RecipeAsync(client, householdId, "Linsensuppe");

        await client.PutAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}", new { }, Token);
        var afterFirst = await client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token);

        // Act
        var second = await client.PutAsync(
            $"/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}",
            new { },
            Token);

        // Assert
        var afterSecond = await client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token);

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(1, afterSecond.Json!.Value.GetProperty("recipeCount").GetInt32());

        // And the version did not move, so every cached copy of the cookbook is
        // still good. Churning it to report that nothing happened would throw
        // them all away.
        Assert.Equal(afterFirst.ETag, afterSecond.ETag);
    }

    [Fact]
    public async Task AddRecipe_ShouldRefuseOneFromAnotherHousehold()
    {
        // Arrange
        using var client = await SignedInAsync();
        var cookbookId = await CookbookAsync(client, await HouseholdAsync(client), "Meins");

        using var stranger = await SecondAccountAsync();
        var theirHousehold = await OwnHouseholdAsync(stranger);
        var theirRecipe = await RecipeAsync(stranger, theirHousehold, "Ihres");

        // Act
        var response = await client.PutAsync(
            $"/api/v1/cookbooks/{cookbookId}/recipes/{theirRecipe}",
            new { },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeletingACookbook_ShouldLeaveItsRecipesAlone()
    {
        // Arrange
        // The whole point: a cookbook is a pointer, and deleting one deletes no
        // food.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var cookbookId = await CookbookAsync(client, householdId, "Sommer");
        var recipeId = await RecipeAsync(client, householdId, "Gazpacho");

        await client.PutAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}", new { }, Token);

        // Act
        var deleted = await client.DeleteAsync($"/api/v1/cookbooks/{cookbookId}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var recipe = await client.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        Assert.Equal(HttpStatusCode.OK, recipe.StatusCode);
    }

    [Fact]
    public async Task DeletingARecipe_ShouldDropItFromEveryCookbook()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var first = await CookbookAsync(client, householdId, "Schnell");
        var second = await CookbookAsync(client, householdId, "Vegetarisch");
        var recipeId = await RecipeAsync(client, householdId, "Ofengemüse");

        await client.PutAsync($"/api/v1/cookbooks/{first}/recipes/{recipeId}", new { }, Token);
        await client.PutAsync($"/api/v1/cookbooks/{second}/recipes/{recipeId}", new { }, Token);

        // Act
        await client.DeleteAsync($"/api/v1/recipes/{recipeId}", Token);

        // Assert
        // A shelf pointing at nothing is worse than a shorter shelf.
        foreach (var cookbookId in new[] { first, second })
        {
            var shelf = await client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token);

            Assert.Equal(0, shelf.Json!.Value.GetProperty("recipeCount").GetInt32());
        }
    }

    [Fact]
    public async Task Delete_ShouldAnswerTheSameWayTwice()
    {
        // Arrange
        using var client = await SignedInAsync();
        var cookbookId = await CookbookAsync(client, await HouseholdAsync(client), "Weg damit");

        await client.DeleteAsync($"/api/v1/cookbooks/{cookbookId}", Token);

        // Act
        var again = await client.DeleteAsync($"/api/v1/cookbooks/{cookbookId}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
    }

    [Fact]
    public async Task Update_ShouldRefuseAStaleIfMatch()
    {
        // Arrange
        using var client = await SignedInAsync();
        var cookbookId = await CookbookAsync(client, await HouseholdAsync(client), "Alt");

        var read = await client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token);
        var stale = read.ETag!;

        await RenameAsync(client, cookbookId, stale, "Neu");

        // Act
        var second = await RenameAsync(client, cookbookId, stale, "Noch neuer");

        // Assert
        Assert.Equal(HttpStatusCode.PreconditionFailed, second.StatusCode);

        var stored = await client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token);

        Assert.Equal("Neu", stored.Json!.Value.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Update_ShouldInsistOnAnIfMatch()
    {
        // Arrange
        using var client = await SignedInAsync();
        var cookbookId = await CookbookAsync(client, await HouseholdAsync(client), "Namenlos");

        // Act
        var response = await client.PatchAsync(
            $"/api/v1/cookbooks/{cookbookId}",
            new { name = "Egal" },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode);
    }

    [Fact]
    public async Task GetForRecipe_ShouldNameEveryShelfItIsOn()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var first = await CookbookAsync(client, householdId, "Abends");
        await CookbookAsync(client, householdId, "Nie benutzt");
        var recipeId = await RecipeAsync(client, householdId, "Risotto");

        await client.PutAsync($"/api/v1/cookbooks/{first}/recipes/{recipeId}", new { }, Token);

        // Act
        var response = await client.GetAsync($"/api/v1/recipes/{recipeId}/cookbooks", Token);

        // Assert
        var named = Assert.Single(response.Json!.Value.GetProperty("items").EnumerateArray());

        Assert.Equal("Abends", named.GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetRecipes_ShouldNameEveryRecipeOnTheShelf_NotOnlyTheFirstPage()
    {
        // Arrange
        // A picker has to mark what is already on before anybody taps it, and
        // a shelf of a hundred is four pages of the recipe list.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var cookbookId = await CookbookAsync(client, householdId, "Backen");
        var on = await RecipeAsync(client, householdId, "Waffeln");
        await RecipeAsync(client, householdId, "Linsensuppe");

        await client.PutAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{on}", new { }, Token);

        // Act
        var response = await client.GetAsync($"/api/v1/cookbooks/{cookbookId}/recipes", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            [on],
            response.Json!.Value.GetProperty("recipeIds").EnumerateArray().Select(id => id.GetGuid()));
    }

    [Fact]
    public async Task GetRecipes_ShouldNotExistForSomebodyElsesHousehold()
    {
        // Arrange
        using var client = await SignedInAsync();
        var cookbookId = await CookbookAsync(client, await HouseholdAsync(client), "Backen");

        using var stranger = await SecondAccountAsync();

        // Act
        var response = await stranger.GetAsync($"/api/v1/cookbooks/{cookbookId}/recipes", Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveRecipe_ShouldSucceedForOneThatWasNeverOn()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var cookbookId = await CookbookAsync(client, householdId, "Leer");
        var recipeId = await RecipeAsync(client, householdId, "Nichts damit zu tun");

        // Act
        var response = await client.DeleteAsync(
            $"/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}",
            Token);

        // Assert
        // Taking off something that was never on is the outcome the caller
        // wanted, and reporting it as a failure would make a retry unsafe.
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task List_ShouldCountTheRecipesOnEachShelf()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var cookbookId = await CookbookAsync(client, householdId, "Zwei drauf");
        await CookbookAsync(client, householdId, "Keins drauf");

        foreach (var title in new[] { "Eins", "Zwei" })
        {
            var recipeId = await RecipeAsync(client, householdId, title);

            await client.PutAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}", new { }, Token);
        }

        // Act
        var response = await client.GetAsync($"/api/v1/cookbooks?householdId={householdId}", Token);

        // Assert
        var items = response.Json!.Value.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(2, response.Json!.Value.GetProperty("total").GetInt32());

        var counted = items.Single(item => item.GetProperty("name").GetString() == "Zwei drauf");
        var empty = items.Single(item => item.GetProperty("name").GetString() == "Keins drauf");

        Assert.Equal(2, counted.GetProperty("recipeCount").GetInt32());

        // A shelf with nothing on it still comes back. It is the one somebody
        // just made and is about to fill.
        Assert.Equal(0, empty.GetProperty("recipeCount").GetInt32());
    }

    private static Task<ApiResponse> RenameAsync(
        ApiClient client,
        Guid cookbookId,
        string etag,
        string name)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/cookbooks/{cookbookId}")
        {
            Content = JsonContent.Create(new { name })
        };

        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(etag));

        return client.SendAsync(request, Token);
    }

    private static async Task<Guid> CookbookAsync(ApiClient client, Guid householdId, string name)
    {
        var created = await client.PostAsync(
            "/api/v1/cookbooks",
            new { householdId, name },
            Token);

        return created.Json!.Value.GetProperty("cookbookId").GetGuid();
    }

    private static async Task<Guid> RecipeAsync(ApiClient client, Guid householdId, string title)
    {
        var created = await client.PostAsync("/api/v1/recipes", new { householdId, title }, Token);

        return created.Json!.Value.GetProperty("recipeId").GetGuid();
    }

    /// <summary>A kitchen of their own, for the account that joined no household.</summary>
    private static async Task<Guid> OwnHouseholdAsync(ApiClient client)
    {
        var created = await client.PostAsync("/api/v1/households", new { name = "Ihre Küche" }, Token);

        return created.Json!.Value.GetProperty("householdId").GetGuid();
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

    private async Task<ApiClient> SecondAccountAsync()
    {
        // The first account is the instance's admin, so registration has to be
        // opened before a second one can exist.
        using var admin = postgres.Api.NewApiClient();

        await admin.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password },
            Token);
        await admin.PutAsync(
            "/api/v1/settings/registration",
            new { openRegistration = true, requireInvitation = false, maxUsers = 100 },
            Token);

        var client = postgres.Api.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email = "grace@example.com", displayName = "Grace", password = Password },
            Token);
        await client.PostAsync(
            "/api/v1/sessions",
            new { email = "grace@example.com", password = Password },
            Token);

        return client;
    }
}
