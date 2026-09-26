using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Infrastructure.Persistence;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Households;

/// <summary>
/// A household that inherits another sees that one's recipes and changes none
/// of them.
/// </summary>
/// <remarks>
/// The cast is always the same: Ada owns her kitchen and the flat that
/// inherits it, and Grace is in the flat and nowhere near Ada's kitchen. What
/// Grace can reach is exactly what inheriting hands out, so every rule is
/// tested from her side.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class InheritanceTests(PostgresFixture postgres)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_ShouldInheritFromTheStart_WhenAskedTo()
    {
        // Arrange
        var ada = await Kitchen.OpenAsync(postgres);

        // Act
        var flat = await CreateAsync(ada.Client, "Flat", inheritsFrom: ada.HouseholdId);

        // Assert
        var me = await ada.Client.GetAsync("/api/v1/users/me", Token);
        var membership = Membership(me, flat);
        var chain = membership.GetProperty("inheritsFrom");

        Assert.Equal(1, chain.GetArrayLength());
        Assert.Equal(ada.HouseholdId, chain[0].GetProperty("householdId").GetGuid());
    }

    [Fact]
    public async Task HeirMember_ShouldFindInheritedRecipes_InTheHeirsLibrary()
    {
        // Arrange
        var (ada, grace, flat, bolognese) = await FlatAsync();

        // Act
        var library = await grace.GetAsync($"/api/v1/recipes?householdId={flat}", Token);
        var search = await grace.GetAsync($"/api/v1/recipes?householdId={flat}&query=bolognese", Token);
        var tags = await grace.GetAsync($"/api/v1/tags?householdId={flat}", Token);

        // Assert
        var found = Assert.Single(Items(library));
        Assert.Equal(bolognese, found.GetProperty("recipeId").GetGuid());
        // Says whose it is, so the card can say it cannot be changed here.
        Assert.Equal(ada.HouseholdId, found.GetProperty("householdId").GetGuid());
        Assert.Contains(Items(search), item => item.GetProperty("recipeId").GetGuid() == bolognese);
        Assert.Contains(Items(tags), tag => tag.GetProperty("slug").GetString() == "pasta");
    }

    [Fact]
    public async Task HeirMember_ShouldReadAnInheritedRecipe_ButNotChangeIt()
    {
        // Arrange
        var (ada, grace, _, bolognese) = await FlatAsync();
        var read = await grace.GetAsync($"/api/v1/recipes/{bolognese}", Token);

        // Act
        var rename = await PutAsync(
            grace,
            $"/api/v1/recipes/{bolognese}",
            read.ETag,
            new { title = "Grace's now", language = "en", yieldAmount = 4, yieldKind = "servings", groups = Array.Empty<object>(), steps = Array.Empty<object>(), tags = Array.Empty<string>() });
        var share = await grace.PutAsync($"/api/v1/recipes/{bolognese}/share", new { }, Token);
        var delete = await grace.DeleteAsync($"/api/v1/recipes/{bolognese}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, rename.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, share.StatusCode);

        // Deleting answers the way it answers for a recipe that is not there,
        // and deletes nothing, which is the part that matters.
        Assert.NotEqual(HttpStatusCode.InternalServerError, delete.StatusCode);
        var stillThere = await ada.Client.GetAsync($"/api/v1/recipes/{bolognese}", Token);
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
        Assert.Equal("Bolognese", stillThere.Json!.Value.GetProperty("title").GetString());
    }

    [Fact]
    public async Task HeirMember_ShouldPlanShelveShopAndCook_AnInheritedRecipe()
    {
        // Arrange
        var (_, grace, flat, bolognese) = await FlatAsync();
        var shelf = (await grace.PostAsync("/api/v1/cookbooks", new { householdId = flat, name = "Weeknights" }, Token))
            .Json!.Value.GetProperty("cookbookId").GetGuid();

        // Act
        var planned = await grace.PostAsync(
            $"/api/v1/households/{flat}/meal-plan",
            new { date = "2026-10-01", recipeId = bolognese },
            Token);
        var shelved = await grace.PutAsync($"/api/v1/cookbooks/{shelf}/recipes/{bolognese}", new { }, Token);
        var listed = await grace.PostAsync(
            $"/api/v1/households/{flat}/shopping-list/recipes",
            new { recipeId = bolognese, servings = 4 },
            Token);
        var cooked = await grace.PostAsync(
            $"/api/v1/recipes/{bolognese}/cook-log",
            new { householdId = flat },
            Token);
        var cooking = await grace.PostAsync(
            "/api/v1/cook-sessions",
            new { recipeId = bolognese, servings = 4, householdId = flat },
            Token);

        // Assert
        Assert.True(planned.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created, $"planning got {(int)planned.StatusCode}");
        Assert.True(shelved.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent, $"shelving got {(int)shelved.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Equal(HttpStatusCode.Created, cooked.StatusCode);
        Assert.Equal(HttpStatusCode.Created, cooking.StatusCode);

        var onShelf = await grace.GetAsync($"/api/v1/recipes/{bolognese}/cookbooks?householdId={flat}", Token);
        Assert.Contains(Items(onShelf), item => item.GetProperty("cookbookId").GetGuid() == shelf);

        // The flat's history, not Ada's kitchen's: stamping the recipe's own
        // household would put Grace into Ada's suggestions by name.
        Assert.Equal(flat, await CookedInAsync(bolognese));
    }

    [Fact]
    public async Task Parent_ShouldNotSeeTheHeirsOwnRecipes()
    {
        // Arrange
        var (ada, grace, flat, _) = await FlatAsync();
        var gracesOwn = await RecipeAsync(grace, flat, "Shakshuka");

        // Act
        var parentLibrary = await ada.Client.GetAsync($"/api/v1/recipes?householdId={ada.HouseholdId}", Token);

        // Assert
        Assert.DoesNotContain(Items(parentLibrary), item => item.GetProperty("recipeId").GetGuid() == gracesOwn);
    }

    [Fact]
    public async Task Inheritance_ShouldCarryThrough_AHouseholdThatInheritsInTurn()
    {
        // Arrange
        var (ada, grace, flat, bolognese) = await FlatAsync();
        var gracesKitchen = await FirstHouseholdIdAsync(grace, except: flat);

        // Act
        var set = await grace.PutAsync(
            $"/api/v1/households/{gracesKitchen}/inheritance",
            new { householdId = flat },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        var chain = set.Json!.Value.GetProperty("inheritsFrom");
        Assert.Equal(flat, chain[0].GetProperty("householdId").GetGuid());
        Assert.Equal(ada.HouseholdId, chain[1].GetProperty("householdId").GetGuid());

        var library = await grace.GetAsync($"/api/v1/recipes?householdId={gracesKitchen}", Token);
        Assert.Contains(Items(library), item => item.GetProperty("recipeId").GetGuid() == bolognese);
    }

    [Fact]
    public async Task SetInheritance_ShouldRefuseALoop()
    {
        // Arrange
        var (ada, _, flat, _) = await FlatAsync();

        // Act
        var response = await ada.Client.PutAsync(
            $"/api/v1/households/{ada.HouseholdId}/inheritance",
            new { householdId = flat },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("households.inheritance_cycle", response.ProblemCode);
    }

    [Fact]
    public async Task SetInheritance_ShouldSayNotFound_ForAHouseholdTheCallerIsNotIn()
    {
        // Arrange
        var (ada, grace, flat, bolognese) = await FlatAsync();
        var gracesKitchen = await FirstHouseholdIdAsync(grace, except: flat);

        // Act
        var response = await grace.PutAsync(
            $"/api/v1/households/{gracesKitchen}/inheritance",
            new { householdId = ada.HouseholdId },
            Token);

        // Assert
        // Inheriting shows a kitchen's recipes to everybody in the heir, so only
        // somebody already in that kitchen may do it — and a stranger learns
        // nothing about whether it exists.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("households.not_found", response.ProblemCode);
        var library = await grace.GetAsync($"/api/v1/recipes?householdId={gracesKitchen}", Token);
        Assert.DoesNotContain(Items(library), item => item.GetProperty("recipeId").GetGuid() == bolognese);
    }

    [Fact]
    public async Task SetInheritance_ShouldBeRefused_ForAPlainMemberOfTheHeir()
    {
        // Arrange
        var (_, grace, flat, _) = await FlatAsync();

        // Act
        var response = await grace.PutAsync(
            $"/api/v1/households/{flat}/inheritance",
            new { householdId = (Guid?)null },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("households.not_owner", response.ProblemCode);
    }

    [Fact]
    public async Task StopInheriting_ShouldTakeTheRecipesAwayAgain()
    {
        // Arrange
        var (ada, grace, flat, bolognese) = await FlatAsync();
        var before = await grace.GetAsync("/api/v1/users/me", Token);

        // Act
        var response = await ada.Client.PutAsync(
            $"/api/v1/households/{flat}/inheritance",
            new { householdId = (Guid?)null },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, response.Json!.Value.GetProperty("inheritsFrom").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await grace.GetAsync($"/api/v1/recipes/{bolognese}", Token)).StatusCode);
        Assert.Empty(Items(await grace.GetAsync($"/api/v1/recipes?householdId={flat}", Token)));

        // The session's own read has to notice, or the app would go on naming
        // a kitchen it no longer inherits from.
        var after = await grace.GetAsync("/api/v1/users/me", Token);
        Assert.NotEqual(before.ETag, after.ETag);
    }

    [Fact]
    public async Task ShoppingList_ShouldRefuseARecipe_ForAHouseholdTheCallerIsNotIn()
    {
        // Arrange
        var (ada, grace, flat, _) = await FlatAsync();
        var gracesKitchen = await FirstHouseholdIdAsync(grace, except: flat);
        var gracesOwn = await RecipeAsync(grace, gracesKitchen, "Shakshuka");

        // Act
        // Her own recipe, into the list of a kitchen she is not in.
        var response = await grace.PostAsync(
            $"/api/v1/households/{ada.HouseholdId}/shopping-list/recipes",
            new { recipeId = gracesOwn, servings = 4 },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var list = await ada.Client.GetAsync($"/api/v1/households/{ada.HouseholdId}/shopping-list", Token);
        Assert.Equal(0, list.Json!.Value.GetProperty("items").GetArrayLength());
    }

    /// <summary>
    /// Ada's kitchen with a Bolognese in it, the flat that inherits it, and
    /// Grace in the flat and in a kitchen of her own.
    /// </summary>
    private async Task<(Kitchen Ada, ApiClient Grace, Guid Flat, Guid Bolognese)> FlatAsync()
    {
        var ada = await Kitchen.OpenAsync(postgres);
        var bolognese = await ada.SaveAsync(
            "Bolognese",
            "en",
            prep: 15,
            cook: 60,
            [("Spaghetti", "g"), ("Beef", "g")],
            ["pasta"],
            "Simmer for an hour.");

        var flat = await CreateAsync(ada.Client, "Flat", inheritsFrom: ada.HouseholdId);
        var grace = await Kitchen.StrangerAsync(postgres);
        await CreateAsync(grace, "Grace's kitchen", inheritsFrom: null);
        await JoinAsync(flat, grace);

        return (ada, grace, flat, bolognese);
    }

    private static async Task<Guid> CreateAsync(ApiClient client, string name, Guid? inheritsFrom) =>
        (await client.PostAsync("/api/v1/households", new { name, inheritsFrom }, Token))
            .Json!.Value.GetProperty("householdId").GetGuid();

    private static async Task<Guid> RecipeAsync(ApiClient client, Guid householdId, string title) =>
        (await client.PostAsync("/api/v1/recipes", new { householdId, title }, Token))
            .Json!.Value.GetProperty("recipeId").GetGuid();

    private static async Task<Guid> FirstHouseholdIdAsync(ApiClient client, Guid except) =>
        Items(await client.GetAsync("/api/v1/households", Token))
            .Select(item => item.GetProperty("householdId").GetGuid())
            .First(id => id != except);

    private static System.Text.Json.JsonElement Membership(ApiResponse me, Guid householdId) =>
        me.Json!.Value.GetProperty("households").EnumerateArray()
            .Single(household => household.GetProperty("householdId").GetGuid() == householdId);

    private static List<System.Text.Json.JsonElement> Items(ApiResponse response) =>
        [.. response.Json!.Value.GetProperty("items").EnumerateArray()];

    private static async Task<ApiResponse> PutAsync(ApiClient client, string path, string? etag, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };

        if (etag is not null)
        {
            request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(etag));
        }

        return await client.SendAsync(request, Token);
    }

    /// <summary>Arranged directly: invitations are tested on their own.</summary>
    private async Task JoinAsync(Guid householdId, ApiClient member)
    {
        var memberId = (await member.GetAsync("/api/v1/users/me", Token)).Json!.Value.GetProperty("userId").GetGuid();

        await using var session = postgres.NewSession();

        await new DbExecutor(session).ExecuteAsync(
            """
            insert into household_members (household_id, user_id, role, joined_at)
            values (@householdId, @memberId, 'member', now());
            """,
            new { householdId, memberId },
            Token);
    }

    private async Task<Guid> CookedInAsync(Guid recipeId)
    {
        await using var session = postgres.NewSession();

        return await new DbExecutor(session).ExecuteScalarAsync<Guid>(
            "select household_id from cook_log_entries where recipe_id = @recipeId;",
            new { recipeId },
            Token);
    }
}
