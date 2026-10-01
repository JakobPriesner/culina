using System.Net;
using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Trash.Purge;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Households;

/// <summary>
/// Deleting puts things in a bin: gone from every list, search and export,
/// back exactly as they were on a restore, and gone for good once the bin is
/// purged — photographs included.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class TrashEndpointTests(PostgresFixture postgres)
{
    private const string Title = "Rhabarberkompott";

    private static readonly DateOnly Monday = new(2026, 10, 5);

    [Fact]
    public async Task DeletedRecipe_ShouldAppearInNoListSearchSuggestionOrExport()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var (recipeId, cookbookId, token) = await EverywhereAsync(kitchen);

        // Act
        await kitchen.Client.DeleteCurrentAsync($"/api/v1/recipes/{recipeId}", Token);

        // Assert
        var client = kitchen.Client;
        var household = kitchen.HouseholdId;

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/recipes/{recipeId}", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/shared-recipes/{token}", Token)).StatusCode);

        foreach (var path in ReadPaths(household, cookbookId))
        {
            var read = await client.GetAsync(path, Token);

            Assert.True(read.StatusCode == HttpStatusCode.OK, $"{path} answered {read.StatusCode}");
            Assert.False(read.Body.Contains(Title, StringComparison.Ordinal), $"{path} still shows the deleted recipe.");
            Assert.False(read.Body.Contains(recipeId.ToString(), StringComparison.Ordinal), $"{path} still names the deleted recipe.");
        }

        var cookbook = await client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token);
        Assert.Equal(0, cookbook.Json!.Value.GetProperty("recipeCount").GetInt32());

        var tags = await client.GetAsync($"/api/v1/tags?householdId={household}", Token);
        Assert.DoesNotContain("kompott", tags.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RestoredRecipe_ShouldBeBackEverywhere_SearchIncluded()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var (recipeId, cookbookId, token) = await EverywhereAsync(kitchen);
        await kitchen.Client.DeleteCurrentAsync($"/api/v1/recipes/{recipeId}", Token);

        // Act
        var restored = await kitchen.Client.PostAsync($"/api/v1/recipes/{recipeId}/restorations", new { }, Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, restored.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await kitchen.Client.GetAsync($"/api/v1/recipes/{recipeId}", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await kitchen.Client.GetAsync($"/api/v1/shared-recipes/{token}", Token)).StatusCode);

        // The search document was dropped on delete; finding it by an
        // ingredient proves it was written again.
        Assert.Contains(recipeId.ToString(), (await kitchen.SearchAsync("Rhabarber")).Body, StringComparison.Ordinal);

        var onShelf = await kitchen.Client.GetAsync(
            $"/api/v1/recipes?householdId={kitchen.HouseholdId}&cookbookId={cookbookId}",
            Token);
        Assert.Contains(recipeId.ToString(), onShelf.Body, StringComparison.Ordinal);

        var plan = await kitchen.Client.GetAsync(
            $"/api/v1/households/{kitchen.HouseholdId}/meal-plan?from={Monday:yyyy-MM-dd}",
            Token);
        Assert.Contains(recipeId.ToString(), plan.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Trash_ShouldListWhatWasDeleted_ByWhomAndUntilWhen()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipeId = await RecipeAsync(kitchen);
        var cookbookId = await CookbookAsync(kitchen, "Sommer");
        await kitchen.Client.DeleteCurrentAsync($"/api/v1/recipes/{recipeId}", Token);
        await kitchen.Client.DeleteCurrentAsync($"/api/v1/cookbooks/{cookbookId}", Token);

        // Act
        var trash = await kitchen.Client.GetAsync($"/api/v1/households/{kitchen.HouseholdId}/trash", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, trash.StatusCode);
        var items = trash.Json!.Value.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);

        var recipe = items.Single(item => item.GetProperty("kind").GetString() == "recipe");
        Assert.Equal(recipeId, recipe.GetProperty("id").GetGuid());
        Assert.Equal(Title, recipe.GetProperty("name").GetString());
        Assert.Equal("Ada", recipe.GetProperty("deletedBy").GetString());
        Assert.Equal(
            recipe.GetProperty("deletedAt").GetDateTimeOffset().AddDays(30),
            recipe.GetProperty("purgeAfter").GetDateTimeOffset());

        var cookbook = items.Single(item => item.GetProperty("kind").GetString() == "cookbook");
        Assert.Equal(cookbookId, cookbook.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Trash_ShouldBeInvisible_ToSomebodyOutsideTheHousehold()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipeId = await RecipeAsync(kitchen);
        await kitchen.Client.DeleteCurrentAsync($"/api/v1/recipes/{recipeId}", Token);
        using var stranger = await Kitchen.StrangerAsync(postgres);

        // Act
        var trash = await stranger.GetAsync($"/api/v1/households/{kitchen.HouseholdId}/trash", Token);
        var restored = await stranger.PostAsync($"/api/v1/recipes/{recipeId}/restorations", new { }, Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, trash.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, restored.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await kitchen.Client.GetAsync($"/api/v1/recipes/{recipeId}", Token)).StatusCode);
    }

    [Fact]
    public async Task Delete_ShouldStillAnswer204_ForARecipeAlreadyInTheBin()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipeId = await RecipeAsync(kitchen);
        var read = await kitchen.Client.GetAsync($"/api/v1/recipes/{recipeId}", Token);
        await kitchen.Client.DeleteAsync($"/api/v1/recipes/{recipeId}", read.ETag!, Token);

        // Act
        var again = await kitchen.Client.DeleteAsync($"/api/v1/recipes/{recipeId}", read.ETag!, Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
    }

    [Fact]
    public async Task Restore_ShouldAnswer404_ForARecipeThatIsNotInTheBin()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipeId = await RecipeAsync(kitchen);

        // Act
        var live = await kitchen.Client.PostAsync($"/api/v1/recipes/{recipeId}/restorations", new { }, Token);
        var unknown = await kitchen.Client.PostAsync($"/api/v1/recipes/{Guid.NewGuid()}/restorations", new { }, Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, live.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task DeletedCookbook_ShouldDisappear_AndComeBackWithItsRecipes()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipeId = await RecipeAsync(kitchen);
        var cookbookId = await CookbookAsync(kitchen, "Sommer");
        await kitchen.Client.PutAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}", new { }, Token);

        // Act
        await kitchen.Client.DeleteCurrentAsync($"/api/v1/cookbooks/{cookbookId}", Token);
        var whileDeleted = await kitchen.Client.GetAsync($"/api/v1/cookbooks?householdId={kitchen.HouseholdId}", Token);
        var restored = await kitchen.Client.PostAsync($"/api/v1/cookbooks/{cookbookId}/restorations", new { }, Token);

        // Assert
        Assert.DoesNotContain(cookbookId.ToString(), whileDeleted.Body, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NoContent, restored.StatusCode);
        var cookbook = await kitchen.Client.GetAsync($"/api/v1/cookbooks/{cookbookId}", Token);
        Assert.Equal(1, cookbook.Json!.Value.GetProperty("recipeCount").GetInt32());
    }

    [Fact]
    public async Task DeletedHousehold_ShouldShutEveryMemberOut_UntilAnOwnerRestoresIt()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipeId = await RecipeAsync(kitchen);
        using var grace = await MemberAsync(kitchen);
        var household = kitchen.HouseholdId;

        // Act
        await kitchen.Client.DeleteCurrentAsync($"/api/v1/households/{household}", Token);

        // Assert
        foreach (var client in new[] { kitchen.Client, grace })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/households/{household}", Token)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/recipes/{recipeId}", Token)).StatusCode);
            Assert.NotEqual(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/households/{household}/shopping-list", Token)).StatusCode);
            Assert.NotEqual(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/recipes?householdId={household}", Token)).StatusCode);
            Assert.DoesNotContain(household.ToString(), (await client.GetAsync("/api/v1/users/me", Token)).Body, StringComparison.Ordinal);
        }

        // Only an owner sees it in the bin, and only an owner may restore it.
        var adasBin = await kitchen.Client.GetAsync("/api/v1/households?deleted=true", Token);
        var gracesBin = await grace.GetAsync("/api/v1/households?deleted=true", Token);
        var deleted = Assert.Single(adasBin.Json!.Value.GetProperty("items").EnumerateArray());
        Assert.Equal(household, deleted.GetProperty("householdId").GetGuid());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, deleted.GetProperty("purgeAfter").ValueKind);
        Assert.Empty(gracesBin.Json!.Value.GetProperty("items").EnumerateArray());

        var byMember = await grace.PostAsync($"/api/v1/households/{household}/restorations", new { }, Token);
        Assert.Equal(HttpStatusCode.Forbidden, byMember.StatusCode);

        var byOwner = await kitchen.Client.PostAsync($"/api/v1/households/{household}/restorations", new { }, Token);
        Assert.Equal(HttpStatusCode.NoContent, byOwner.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await grace.GetAsync($"/api/v1/recipes/{recipeId}", Token)).StatusCode);
    }

    [Fact]
    public async Task RestoringAHousehold_ShouldNotBringBackWhatWasDeletedInsideItEarlier()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var kept = await RecipeAsync(kitchen);
        var binned = await kitchen.SaveAsync("Alter Eintopf", "de", 10, 60, [("Kartoffeln", "g")], [], "Kochen.");
        await kitchen.Client.DeleteCurrentAsync($"/api/v1/recipes/{binned}", Token);
        await kitchen.Client.DeleteCurrentAsync($"/api/v1/households/{kitchen.HouseholdId}", Token);

        // Act
        await kitchen.Client.PostAsync($"/api/v1/households/{kitchen.HouseholdId}/restorations", new { }, Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, (await kitchen.Client.GetAsync($"/api/v1/recipes/{kept}", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await kitchen.Client.GetAsync($"/api/v1/recipes/{binned}", Token)).StatusCode);
    }

    [Fact]
    public async Task Purge_ShouldRemoveWhatIsOlderThanTheRetention_WithItsPicture()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var old = await RecipeAsync(kitchen);
        var recent = await kitchen.SaveAsync("Frischer Salat", "de", 10, null, [("Gurke", null)], [], "Schneiden.");
        await SetImageAsync(kitchen, old, TestImages.Png(640, 480));
        var hash = await postgres.QuerySingleAsync<string>($"select content_hash from recipe_images where recipe_id = '{old}';", Token);

        await kitchen.Client.DeleteCurrentAsync($"/api/v1/recipes/{old}", Token);
        await kitchen.Client.DeleteCurrentAsync($"/api/v1/recipes/{recent}", Token);
        await postgres.ExecuteAsync(
            $"update recipes_with_deleted set deleted_at = now() - interval '31 days' where id = '{old}';",
            Token);

        // Act
        var removed = await PurgeAsync();

        // Assert
        Assert.Equal(1, removed);
        Assert.Equal(0, await CountAsync($"select count(*) from recipes_with_deleted where id = '{old}';"));
        Assert.Equal(1, await CountAsync($"select count(*) from recipes_with_deleted where id = '{recent}';"));
        Assert.Equal(HttpStatusCode.NotFound, (await kitchen.Client.PostAsync($"/api/v1/recipes/{old}/restorations", new { }, Token)).StatusCode);
        Assert.False(await ImageExistsAsync(hash), "The purged recipe's picture is still in storage.");
    }

    [Fact]
    public async Task Purge_ShouldKeepAPicture_ThatAnotherRecipeStillUses()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var gone = await RecipeAsync(kitchen);
        var stays = await kitchen.SaveAsync("Frischer Salat", "de", 10, null, [("Gurke", null)], [], "Schneiden.");
        var picture = TestImages.Png(640, 480);
        await SetImageAsync(kitchen, gone, picture);
        await SetImageAsync(kitchen, stays, picture);
        var hash = await postgres.QuerySingleAsync<string>($"select content_hash from recipe_images where recipe_id = '{gone}';", Token);

        await kitchen.Client.DeleteCurrentAsync($"/api/v1/recipes/{gone}", Token);
        await postgres.ExecuteAsync("update recipes_with_deleted set deleted_at = now() - interval '31 days' where deleted_at is not null;", Token);

        // Act
        await PurgeAsync();

        // Assert
        Assert.True(await ImageExistsAsync(hash), "A picture another recipe shows was deleted with the purged one.");
    }

    [Fact]
    public async Task Purge_ShouldRemoveADeletedHousehold_AndEverythingInIt()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipeId = await RecipeAsync(kitchen);
        await kitchen.Client.DeleteCurrentAsync($"/api/v1/households/{kitchen.HouseholdId}", Token);
        await postgres.ExecuteAsync("update households_with_deleted set deleted_at = now() - interval '31 days';", Token);

        // Act
        await PurgeAsync();

        // Assert
        Assert.Equal(0, await CountAsync($"select count(*) from households_with_deleted where id = '{kitchen.HouseholdId}';"));
        Assert.Equal(0, await CountAsync($"select count(*) from recipes_with_deleted where id = '{recipeId}';"));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Every list a recipe can appear in, as the app reads them.</summary>
    private static IEnumerable<string> ReadPaths(Guid household, Guid cookbookId) =>
    [
        $"/api/v1/recipes?householdId={household}",
        $"/api/v1/recipes?householdId={household}&query=Rhabarber",
        $"/api/v1/recipes?householdId={household}&query=Rhabarberkompot",
        $"/api/v1/recipes?householdId={household}&tag=kompott",
        $"/api/v1/recipes?householdId={household}&sort=suggested",
        $"/api/v1/recipes?householdId={household}&cookbookId={cookbookId}",
        $"/api/v1/households/{household}/completions?query=Rhab",
        $"/api/v1/suggestions?householdId={household}",
        $"/api/v1/households/{household}/meal-plan?from={Monday:yyyy-MM-dd}",
        $"/api/v1/households/{household}/archive",
        $"/api/v1/cookbooks?householdId={household}"
    ];

    /// <summary>A recipe on a shelf, in the plan and shared by link: in every place a delete must reach.</summary>
    private static async Task<(Guid RecipeId, Guid CookbookId, string Token)> EverywhereAsync(Kitchen kitchen)
    {
        var recipeId = await RecipeAsync(kitchen);
        var cookbookId = await CookbookAsync(kitchen, "Sommer");

        await kitchen.Client.PutAsync($"/api/v1/cookbooks/{cookbookId}/recipes/{recipeId}", new { }, Token);
        await kitchen.Client.PostAsync(
            $"/api/v1/households/{kitchen.HouseholdId}/meal-plan",
            new { date = Monday, recipeId },
            Token);

        var shared = await kitchen.Client.PutAsync($"/api/v1/recipes/{recipeId}/share", new { }, Token);

        return (recipeId, cookbookId, shared.Json!.Value.GetProperty("token").GetString()!);
    }

    private static Task<Guid> RecipeAsync(Kitchen kitchen) =>
        kitchen.SaveAsync(Title, "de", 15, 20, [("Rhabarber", "g"), ("Zucker", "g")], ["Kompott"], "Rhabarber schneiden und kochen.");

    private static async Task<Guid> CookbookAsync(Kitchen kitchen, string name)
    {
        var created = await kitchen.Client.PostAsync(
            "/api/v1/cookbooks",
            new { householdId = kitchen.HouseholdId, name },
            Token);

        return created.Json!.Value.GetProperty("cookbookId").GetGuid();
    }

    /// <summary>Grace, invited into Ada's household.</summary>
    private async Task<ApiClient> MemberAsync(Kitchen kitchen)
    {
        var grace = await Kitchen.StrangerAsync(postgres);
        var invitation = await kitchen.Client.PostAsync($"/api/v1/households/{kitchen.HouseholdId}/invitations", new { }, Token);
        var code = invitation.Json!.Value.GetProperty("code").GetString()!;

        await grace.PostAsync($"/api/v1/invitations/{code}/redemptions", new { }, Token);

        return grace;
    }

    private static async Task SetImageAsync(Kitchen kitchen, Guid recipeId, byte[] picture)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(picture);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "picture.png");

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{recipeId}/image") { Content = content };
        var response = await kitchen.Client.SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<int> PurgeAsync()
    {
        await using var scope = postgres.Api.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<PurgeTrashCommand, int>>();

        var result = await handler.Handle(new PurgeTrashCommand(DateTimeOffset.UtcNow), Token);

        return result.Match(removed => removed, error => throw new InvalidOperationException(error.Code));
    }

    private async Task<int> CountAsync(string sql) =>
        (int)await postgres.QuerySingleAsync<long>(sql, Token);

    private async Task<bool> ImageExistsAsync(string hash)
    {
        await using var scope = postgres.Api.Services.CreateAsyncScope();
        var images = scope.ServiceProvider.GetRequiredService<IImageStore>();
        using var copy = new MemoryStream();

        var result = await images.CopyToAsync(hash, Application.Abstractions.ImageWidths.Card, copy, Token);

        return result.Match(() => true, _ => false);
    }
}
