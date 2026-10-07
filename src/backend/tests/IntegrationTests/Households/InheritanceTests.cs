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
        var delete = await grace.DeleteAsync($"/api/v1/recipes/{bolognese}", read.ETag!, Token);

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

    [Fact]
    public async Task Heirs_ShouldListEveryHouseholdThatSeesTheRecipes_ForAnyMember()
    {
        // Arrange
        var (ada, grace, flat, _) = await FlatAsync();
        var gracesKitchen = await GracesKitchenInheritingAsync(grace, flat);

        // Act
        var heirs = await ada.Client.GetAsync($"/api/v1/households/{ada.HouseholdId}/heirs", Token);
        var stranger = await grace.GetAsync($"/api/v1/households/{ada.HouseholdId}/heirs", Token);

        // Assert
        // Through the flat too: Grace's kitchen reads Ada's recipes, so Ada's
        // kitchen is told about it.
        var items = Items(heirs);
        Assert.Equal([flat, gracesKitchen], items.Select(item => item.GetProperty("householdId").GetGuid()));
        Assert.Equal(ada.HouseholdId, items[0].GetProperty("inheritsFrom").GetGuid());
        Assert.Equal(flat, items[1].GetProperty("inheritsFrom").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);
    }

    [Fact]
    public async Task RemoveHeir_ShouldLetAnOwnerOfTheParentCutLooseAHouseholdTheyAreNotIn()
    {
        // Arrange
        var (ada, grace, flat, bolognese) = await FlatAsync();
        var gracesKitchen = await GracesKitchenInheritingAsync(grace, flat);

        // Act
        // Ada owns the flat and is nowhere near Grace's kitchen.
        var response = await ada.Client.DeleteAsync($"/api/v1/households/{flat}/heirs/{gracesKitchen}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var library = await grace.GetAsync($"/api/v1/recipes?householdId={gracesKitchen}", Token);
        Assert.DoesNotContain(Items(library), item => item.GetProperty("recipeId").GetGuid() == bolognese);
        var heirs = await ada.Client.GetAsync($"/api/v1/households/{flat}/heirs", Token);
        Assert.Empty(Items(heirs));
    }

    [Fact]
    public async Task RemoveHeir_ShouldBeRefused_ForAPlainMemberOfTheParent()
    {
        // Arrange
        var (_, grace, flat, _) = await FlatAsync();
        var gracesKitchen = await GracesKitchenInheritingAsync(grace, flat);

        // Act
        var response = await grace.DeleteAsync($"/api/v1/households/{flat}/heirs/{gracesKitchen}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("households.not_owner", response.ProblemCode);
    }

    [Fact]
    public async Task RemoveHeir_ShouldSayNotFound_ForAHouseholdThatInheritsThroughAnother()
    {
        // Arrange
        var (ada, grace, flat, _) = await FlatAsync();
        var gracesKitchen = await GracesKitchenInheritingAsync(grace, flat);

        // Act
        var response = await ada.Client.DeleteAsync(
            $"/api/v1/households/{ada.HouseholdId}/heirs/{gracesKitchen}",
            Token);

        // Assert
        // That link is the flat's to cut; cutting the flat would take it too.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("households.not_found", response.ProblemCode);
    }

    [Fact]
    public async Task RemovingAMember_ShouldEndTheInheritanceTheySetUp_AndNoOtherOne()
    {
        // Arrange
        // Grace was a plain member of Ada's kitchen and pointed a household of
        // her own at it. Ada's flat inherits it too, by Ada's own choice.
        var (ada, grace, graceId, gracesKitchen, bolognese) = await GraceInheritingAdasKitchenAsync();
        var flat = await CreateAsync(ada.Client, "Flat", inheritsFrom: ada.HouseholdId);
        var whileInside = await grace.GetAsync($"/api/v1/recipes/{bolognese}/image", Token);

        // Act
        var removed = await ada.Client.DeleteAsync($"/api/v1/households/{ada.HouseholdId}/members/{graceId}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, whileInside.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        await AssertCutOffAsync(grace, gracesKitchen, bolognese);

        // Ada is still in her kitchen, so the flat she pointed at it keeps it.
        var flatLibrary = await ada.Client.GetAsync($"/api/v1/recipes?householdId={flat}", Token);
        Assert.Contains(Items(flatLibrary), item => item.GetProperty("recipeId").GetGuid() == bolognese);
    }

    [Fact]
    public async Task LeavingAHousehold_ShouldEndTheInheritanceTheLeaverSetUp()
    {
        // Arrange
        var (ada, grace, graceId, gracesKitchen, bolognese) = await GraceInheritingAdasKitchenAsync();

        // Act
        var left = await grace.DeleteAsync($"/api/v1/households/{ada.HouseholdId}/members/{graceId}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, left.StatusCode);
        await AssertCutOffAsync(grace, gracesKitchen, bolognese);
    }

    [Fact]
    public async Task CookSession_ShouldNameARecipeNoLongerInherited_ToNobody()
    {
        // Arrange
        var (ada, grace, flat, bolognese) = await FlatAsync();
        var started = await grace.PostAsync(
            "/api/v1/cook-sessions",
            new { recipeId = bolognese, servings = 4, householdId = flat },
            Token);
        var sessionId = started.Json!.Value.GetProperty("sessionId").GetGuid();

        // Act
        await ada.Client.PutAsync($"/api/v1/households/{flat}/inheritance", new { householdId = (Guid?)null }, Token);
        var current = await grace.GetAsync("/api/v1/cook-sessions/current", Token);
        var moved = await grace.PatchAsync($"/api/v1/cook-sessions/{sessionId}", new { currentStepIndex = 0 }, Token);

        // Assert
        // Revoked access takes effect at once, the resume bar's title included.
        Assert.Equal(HttpStatusCode.Created, started.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, current.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, moved.StatusCode);
        Assert.DoesNotContain("Bolognese", current.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Bolognese", moved.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShoppingList_ShouldKeepTheAmounts_ButNotNameARecipeNoLongerInherited()
    {
        // Arrange
        var (ada, grace, flat, bolognese) = await FlatAsync();
        await grace.PostAsync(
            $"/api/v1/households/{flat}/shopping-list/recipes",
            new { recipeId = bolognese, servings = 4 },
            Token);
        var before = await grace.GetAsync($"/api/v1/households/{flat}/shopping-list", Token);

        // Act
        await ada.Client.PutAsync($"/api/v1/households/{flat}/inheritance", new { householdId = (Guid?)null }, Token);
        var after = await grace.GetAsync($"/api/v1/households/{flat}/shopping-list", Token);

        // Assert
        Assert.Contains("Bolognese", before.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Bolognese", after.Body, StringComparison.Ordinal);
        Assert.Equal(
            before.Json!.Value.GetProperty("items").GetArrayLength(),
            after.Json!.Value.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Copy_ShouldGiveTheHeirARecipeOfItsOwn_ThatItCanChange()
    {
        // Arrange
        var (ada, grace, flat, bolognese) = await FlatAsync();

        // Act
        var copied = await grace.PostAsync($"/api/v1/recipes/{bolognese}/copies", new { householdId = flat }, Token);

        // Assert
        Assert.Equal(HttpStatusCode.Created, copied.StatusCode);
        var copy = copied.Json!.Value;
        var copyId = copy.GetProperty("recipeId").GetGuid();
        Assert.NotEqual(bolognese, copyId);
        Assert.Equal(flat, copy.GetProperty("householdId").GetGuid());
        Assert.Equal("Bolognese", copy.GetProperty("title").GetString());

        var read = await grace.GetAsync($"/api/v1/recipes/{copyId}", Token);
        var renamed = await PutAsync(
            grace,
            $"/api/v1/recipes/{copyId}",
            read.ETag,
            new { title = "Grace's Bolognese", language = "en", yieldAmount = 4, yieldKind = "servings", groups = Array.Empty<object>(), steps = Array.Empty<object>(), tags = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);

        // The original is Ada's, and is exactly as she wrote it.
        var original = await ada.Client.GetAsync($"/api/v1/recipes/{bolognese}", Token);
        Assert.Equal("Bolognese", original.Json!.Value.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Copy_ShouldSayNotFound_ForAHouseholdTheCallerIsNotIn()
    {
        // Arrange
        var (ada, grace, _, bolognese) = await FlatAsync();

        // Act
        var response = await grace.PostAsync(
            $"/api/v1/recipes/{bolognese}/copies",
            new { householdId = ada.HouseholdId },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Plan_ShouldDropAMealOfARecipeNoLongerInherited_AndBringItBackWithIt()
    {
        // Arrange
        var (ada, grace, flat, bolognese) = await FlatAsync();
        await grace.PostAsync(
            $"/api/v1/households/{flat}/meal-plan",
            new { date = "2026-10-01", recipeId = bolognese },
            Token);

        // Act
        await ada.Client.PutAsync($"/api/v1/households/{flat}/inheritance", new { householdId = (Guid?)null }, Token);
        var cut = await grace.GetAsync($"/api/v1/households/{flat}/meal-plan?from=2026-10-01", Token);

        await ada.Client.PutAsync($"/api/v1/households/{flat}/inheritance", new { householdId = ada.HouseholdId }, Token);
        var restored = await grace.GetAsync($"/api/v1/households/{flat}/meal-plan?from=2026-10-01", Token);

        // Assert
        Assert.DoesNotContain(bolognese, Planned(cut));
        Assert.Contains(bolognese, Planned(restored));
    }

    [Fact]
    public async Task Suggestions_ShouldSayWhoseAnInheritedRecipeIs()
    {
        // Arrange
        var (ada, grace, flat, bolognese) = await FlatAsync();

        // Act
        var suggestions = await grace.GetAsync($"/api/v1/suggestions?householdId={flat}", Token);

        // Assert
        var suggested = Assert.Single(Items(suggestions), item => item.GetProperty("recipeId").GetGuid() == bolognese);
        Assert.Equal(ada.HouseholdId, suggested.GetProperty("householdId").GetGuid());
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

    /// <summary>
    /// Ada's kitchen with a photographed Bolognese in it, Grace as a plain
    /// member of it, and a kitchen of Grace's own that she made inherit it.
    /// </summary>
    private async Task<(Kitchen Ada, ApiClient Grace, Guid GraceId, Guid GracesKitchen, Guid Bolognese)>
        GraceInheritingAdasKitchenAsync()
    {
        var ada = await Kitchen.OpenAsync(postgres);
        var bolognese = await RecipeAsync(ada.Client, ada.HouseholdId, "Bolognese");
        await ada.PictureAsync(bolognese, TestImages.Png(2, 2));

        var grace = await Kitchen.StrangerAsync(postgres);
        var graceId = await JoinAsync(ada.HouseholdId, grace);
        var gracesKitchen = await CreateAsync(grace, "Grace's kitchen", inheritsFrom: ada.HouseholdId);

        return (ada, grace, graceId, gracesKitchen, bolognese);
    }

    /// <summary>
    /// Nothing of the recipe reaches Grace any more: not in her kitchen's
    /// library, not by id, not its picture, and not as a copy of her own.
    /// </summary>
    private static async Task AssertCutOffAsync(ApiClient grace, Guid gracesKitchen, Guid recipeId)
    {
        var library = await grace.GetAsync($"/api/v1/recipes?householdId={gracesKitchen}", Token);
        var read = await grace.GetAsync($"/api/v1/recipes/{recipeId}", Token);
        var image = await grace.GetAsync($"/api/v1/recipes/{recipeId}/image", Token);
        var copy = await grace.PostAsync($"/api/v1/recipes/{recipeId}/copies", new { householdId = gracesKitchen }, Token);
        var me = await grace.GetAsync("/api/v1/users/me", Token);

        Assert.DoesNotContain(Items(library), item => item.GetProperty("recipeId").GetGuid() == recipeId);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, image.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, copy.StatusCode);
        Assert.Equal(0, Membership(me, gracesKitchen).GetProperty("inheritsFrom").GetArrayLength());
    }

    /// <summary>Grace's own kitchen, made to inherit the flat.</summary>
    private static async Task<Guid> GracesKitchenInheritingAsync(ApiClient grace, Guid flat)
    {
        var kitchen = await FirstHouseholdIdAsync(grace, except: flat);
        var set = await grace.PutAsync($"/api/v1/households/{kitchen}/inheritance", new { householdId = flat }, Token);

        Assert.Equal(HttpStatusCode.OK, set.StatusCode);

        return kitchen;
    }

    private static List<Guid> Planned(ApiResponse week) =>
        [.. week.Json!.Value.GetProperty("days").EnumerateArray()
            .SelectMany(day => day.GetProperty("meals").EnumerateArray())
            .Select(meal => meal.GetProperty("recipeId").GetGuid())];

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
    private async Task<Guid> JoinAsync(Guid householdId, ApiClient member)
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

        return memberId;
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
