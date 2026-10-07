using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Cookbooks;

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

        // The version must not move, or every cached copy of the cookbook is thrown away.
        Assert.Equal(afterFirst.ETag, afterSecond.ETag);
    }

    [Fact]
    public async Task Cover_ShouldNameThePictureAndChangeTheTag_WhenAPictureIsReplaced()
    {
        // Arrange
        // The cover URL embeds the picture id, and replacing a picture does not touch the cookbook's
        // version, so the tag must cover the id too or a stale 304 would hide the new cover.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var cookbookId = await CookbookAsync(client, householdId, "Wochentags");
        var recipeId = await RecipeAsync(client, householdId, "Linsensuppe");

        await client.PutAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}", new { }, Token);
        var firstImageId = await PhotographAsync(client, recipeId, TestImages.Png(620, 420));
        var before = await client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token);

        // Act
        var secondImageId = await PhotographAsync(client, recipeId, TestImages.Png(630, 430));

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/cookbooks/{cookbookId}");
        request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(before.ETag!));
        var after = await client.SendAsync(request, Token);

        // Assert
        Assert.Equal(firstImageId, CoverImageId(before));
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Equal(secondImageId, CoverImageId(after));
        Assert.NotEqual(before.ETag, after.ETag);
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
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var cookbookId = await CookbookAsync(client, householdId, "Sommer");
        var recipeId = await RecipeAsync(client, householdId, "Gazpacho");

        await client.PutAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}", new { }, Token);

        // Act
        var deleted = await client.DeleteCurrentAsync($"/api/v1/cookbooks/{cookbookId}", Token);

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
        await client.DeleteCurrentAsync($"/api/v1/recipes/{recipeId}", Token);

        // Assert
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
        var etag = (await client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token)).ETag!;

        await client.DeleteAsync($"/api/v1/cookbooks/{cookbookId}", etag, Token);

        // Act
        var again = await client.DeleteAsync($"/api/v1/cookbooks/{cookbookId}", etag, Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
    }

    [Fact]
    public async Task Delete_ShouldRequireIfMatch_AndRejectAStaleOne()
    {
        // Arrange
        using var client = await SignedInAsync();
        var cookbookId = await CookbookAsync(client, await HouseholdAsync(client), "Bleibt");
        var stale = (await client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token)).ETag!;

        await RenameAsync(client, cookbookId, stale, "Umbenannt");

        // Act
        var missing = await client.DeleteAsync($"/api/v1/cookbooks/{cookbookId}", Token);
        var outdated = await client.DeleteAsync($"/api/v1/cookbooks/{cookbookId}", stale, Token);

        // Assert
        Assert.Equal(HttpStatusCode.PreconditionRequired, missing.StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, outdated.StatusCode);

        var stored = await client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token);

        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
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
        // A picker marks what is already on, so this must not be paged.
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
        // Already-off is the wanted outcome; failing would make a retry unsafe.
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

        // An empty shelf still comes back: it is the one just made.
        Assert.Equal(0, empty.GetProperty("recipeCount").GetInt32());
    }

    [Fact]
    public async Task List_ShouldAnswer_WhenACursorsTimeCarriesAnOffset()
    {
        // Arrange
        // Npgsql refuses non-UTC timestamptz, so a hand-made +02:00 cursor must be normalised.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        await CookbookAsync(client, householdId, "Sonntag");
        var cursor = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
            $$"""{"UpdatedAt":"2999-01-01T00:00:00+02:00","Id":"{{Guid.Empty}}"}"""));

        // Act
        var response = await client.GetAsync(
            $"/api/v1/cookbooks?householdId={householdId}&cursor={cursor}",
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(response.Json!.Value.GetProperty("items").EnumerateArray());
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

    /// <summary>Gives a recipe a picture, and says which one it now has.</summary>
    private static async Task<Guid> PhotographAsync(ApiClient client, Guid recipeId, byte[] png)
    {
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");

        var content = new MultipartFormDataContent { { file, "file", "photo.png" } };
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{recipeId}/image")
        {
            Content = content
        };

        var uploaded = await client.SendAsync(request, Token);

        return uploaded.Json!.Value.GetProperty("imageId").GetGuid();
    }

    private static Guid CoverImageId(ApiResponse cookbook)
    {
        var picture = Assert.Single(cookbook.Json!.Value.GetProperty("coverPictures").EnumerateArray());

        return picture.GetProperty("imageId").GetGuid();
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
        // The first account is the admin; registration must be opened for a second.
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
