using System.Net;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Recipes;

/// <summary>Asking the assistant over the wire on an instance that has none, the state nearly every instance runs in.</summary>
[Collection(RequiresDatabase.Name)]
public class RecipeDraftEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task Draft_ShouldSayThereIsNothingHere_WhenNoAssistantIsConnected()
    {
        var world = await SignedInAsync();

        var response = await world.Client.PostAsync(
            "/api/v1/recipe-drafts",
            new
            {
                kind = "idea",
                householdId = world.HouseholdId,
                material = "something with aubergines",
                language = "en"
            },
            Token);

        // Not found rather than forbidden: with no assistant the feature does not exist.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("assistance.not_configured", response.ProblemCode);
    }

    [Fact]
    public async Task Draft_ShouldRefuseAKitchenTheCallerIsNotIn_BeforeSpendingAnything()
    {
        var world = await SignedInAsync();

        var response = await world.Client.PostAsync(
            "/api/v1/recipe-drafts",
            new
            {
                kind = "idea",
                householdId = Guid.CreateVersion7(),
                material = "something with aubergines",
                language = "en"
            },
            Token);

        // The access check runs before the assistant, so strangers cannot make the instance spend money.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Revision_ShouldBeRefused_WhenBilledToAnotherOfTheCallersKitchens()
    {
        // A cook in two households may edit the recipe and spend in both, but not one kitchen's money on the other's recipe.
        var world = await SignedInAsync();
        var recipeId = await RecipeAsync(world);
        var holidayFlat = (await world.Client.PostAsync(
                "/api/v1/households",
                new { name = "Holiday flat" },
                Token))
            .Json!.Value.GetProperty("householdId").GetGuid();

        var response = await world.Client.PostAsync(
            "/api/v1/recipe-drafts",
            new { kind = "revision", householdId = holidayFlat, recipeId },
            Token);

        // Decided before the assistant is consulted, or this would answer "not configured".
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("recipes.not_found", response.ProblemCode);
    }

    [Fact]
    public async Task Revision_ShouldGetPastTheRecipeCheck_WhenBilledToTheRecipesOwnKitchen()
    {
        var world = await SignedInAsync();
        var recipeId = await RecipeAsync(world);

        var response = await world.Client.PostAsync(
            "/api/v1/recipe-drafts",
            new { kind = "revision", householdId = world.HouseholdId, recipeId },
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("assistance.not_configured", response.ProblemCode);
    }

    [Fact]
    public async Task Draft_ShouldBeRefused_ForSomebodyWhoIsNotSignedIn()
    {
        await postgres.ResetAsync(Token);
        using var stranger = postgres.Api.NewApiClient();

        var response = await stranger.PostAsync(
            "/api/v1/recipe-drafts",
            new { kind = "idea", householdId = Guid.CreateVersion7(), language = "en" },
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CurrentUser_ShouldSayNoCapabilityIsAvailable_OnAFreshInstance()
    {
        var world = await SignedInAsync();

        var me = await world.Client.GetAsync("/api/v1/users/me", Token);

        // What every screen reads to decide whether to draw an assistant button.
        var assistance = me.Json!.Value.GetProperty("assistance");
        Assert.False(assistance.GetProperty("improve").GetBoolean());
        Assert.False(assistance.GetProperty("draft").GetBoolean());
        Assert.False(assistance.GetProperty("read").GetBoolean());
        Assert.False(assistance.GetProperty("draw").GetBoolean());
    }

    [Fact]
    public async Task Create_ShouldRecordThatARecipeWasDrafted_WhenItCarriesADraftId()
    {
        var world = await SignedInAsync();
        var draftId = Guid.CreateVersion7();

        var created = await world.Client.PostAsync(
            "/api/v1/recipes",
            new { householdId = world.HouseholdId, title = "Aubergine bake", draftId },
            Token);

        // The same provenance an imported recipe carries, in the same table.
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var recipeId = created.Json!.Value.GetProperty("recipeId").GetGuid();
        var read = await world.Client.GetAsync($"/api/v1/recipes/{recipeId}", Token);
        var origin = read.Json!.Value.GetProperty("origin");

        Assert.Equal("ai", origin.GetProperty("kind").GetString());
        Assert.Equal(draftId.ToString(), origin.GetProperty("externalId").GetString());
    }

    [Fact]
    public async Task Create_ShouldRecordNothing_ForARecipeSomebodyTyped()
    {
        var world = await SignedInAsync();

        var created = await world.Client.PostAsync(
            "/api/v1/recipes",
            new { householdId = world.HouseholdId, title = "Aubergine bake" },
            Token);

        var recipeId = created.Json!.Value.GetProperty("recipeId").GetGuid();
        var read = await world.Client.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        // A recipe written here has no origin at all.
        Assert.False(read.Json!.Value.TryGetProperty("origin", out var origin) && origin.ValueKind is not JsonValueKind.Null);
    }

    [Fact]
    public async Task ImportedRecipe_ShouldKeepItsOriginalLink_InTheExistingProvenance()
    {
        using var world = await SignedInAsync();
        var created = await world.Client.PostAsync("/api/v1/recipes",
            new { householdId = world.HouseholdId, title = "Beans", sourceUrl = "https://example.com/beans", draftId = Guid.CreateVersion7() }, Token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var recipeId = created.Json!.Value.GetProperty("recipeId").GetGuid();
        var read = await world.Client.GetAsync($"/api/v1/recipes/{recipeId}", Token);
        var origin = read.Json!.Value.GetProperty("origin");
        Assert.Equal("web", origin.GetProperty("kind").GetString());
        Assert.Equal("https://example.com/beans", origin.GetProperty("sourceUrl").GetString());
    }

    [Fact]
    public async Task ImportedRecipe_ShouldRefuseAScriptLink_BeforeCreatingAnything()
    {
        using var world = await SignedInAsync();
        var created = await world.Client.PostAsync("/api/v1/recipes",
            new { householdId = world.HouseholdId, title = "Beans", sourceUrl = "javascript:alert(1)" }, Token);
        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
    }

    [Fact]
    public async Task ImportedRecipe_ShouldShowNoLink_WhenTheStoredOriginalIsNotAWebAddress()
    {
        // A row from before the rule, as a connected Tandoor could have sent it: a script labelled with a trusted host.
        using var world = await SignedInAsync();
        var created = await world.Client.PostAsync("/api/v1/recipes",
            new { householdId = world.HouseholdId, title = "Beans", sourceUrl = "https://chefkoch.de/beans" }, Token);
        var recipeId = created.Json!.Value.GetProperty("recipeId").GetGuid();
        await postgres.ExecuteAsync(
            $"update recipe_origins set source_url = 'javascript://chefkoch.de/%0aalert(1)' where recipe_id = '{recipeId}';",
            Token);

        var read = await world.Client.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        var origin = read.Json!.Value.GetProperty("origin");
        Assert.Equal("web", origin.GetProperty("kind").GetString());
        Assert.False(origin.TryGetProperty("sourceUrl", out var link) && link.ValueKind is not JsonValueKind.Null);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<Guid> RecipeAsync(World world) =>
        (await world.Client.PostAsync(
            "/api/v1/recipes",
            new { householdId = world.HouseholdId, title = "Bolognese" },
            Token))
        .Json!.Value.GetProperty("recipeId").GetGuid();

    private sealed record World(ApiClient Client, Guid HouseholdId) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }

    private async Task<World> SignedInAsync()
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

        var householdId = (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();

        return new World(client, householdId);
    }
}
