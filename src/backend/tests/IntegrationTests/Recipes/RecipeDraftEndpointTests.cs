using System.Net;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Recipes;

/// <summary>
/// Asking the assistant over the wire, on an instance that has none.
/// </summary>
/// <remarks>
/// The state nearly every Culina runs in, and the one the whole feature
/// promises to be invisible in — so it is the one worth proving at the wire
/// rather than only in a handler test.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class RecipeDraftEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task Draft_ShouldSayThereIsNothingHere_WhenNoAssistantIsConnected()
    {
        // Arrange
        var world = await SignedInAsync();

        // Act
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

        // Assert
        // Not found rather than forbidden: an instance with no assistant is one
        // where the thing does not exist, and the affordance that would have
        // asked is not on screen either.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("assistance.not_configured", response.ProblemCode);
    }

    [Fact]
    public async Task Draft_ShouldRefuseAKitchenTheCallerIsNotIn_BeforeSpendingAnything()
    {
        // Arrange
        var world = await SignedInAsync();

        // Act
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

        // Assert
        // The access check runs before the assistant is consulted, so a
        // stranger cannot make this instance spend money by asking about a
        // household that is not theirs.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Draft_ShouldBeRefused_ForSomebodyWhoIsNotSignedIn()
    {
        // Arrange
        await postgres.ResetAsync(Token);
        using var stranger = postgres.Api.NewApiClient();

        // Act
        var response = await stranger.PostAsync(
            "/api/v1/recipe-drafts",
            new { kind = "idea", householdId = Guid.CreateVersion7(), language = "en" },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CurrentUser_ShouldSayNoCapabilityIsAvailable_OnAFreshInstance()
    {
        // Arrange
        var world = await SignedInAsync();

        // Act
        var me = await world.Client.GetAsync("/api/v1/users/me", Token);

        // Assert
        // What every screen reads to decide whether to draw an assistant
        // button. All false means the app looks exactly as it did before any of
        // this existed.
        var assistance = me.Json!.Value.GetProperty("assistance");
        Assert.False(assistance.GetProperty("improve").GetBoolean());
        Assert.False(assistance.GetProperty("draft").GetBoolean());
        Assert.False(assistance.GetProperty("read").GetBoolean());
        Assert.False(assistance.GetProperty("draw").GetBoolean());
    }

    [Fact]
    public async Task Create_ShouldRecordThatARecipeWasDrafted_WhenItCarriesADraftId()
    {
        // Arrange
        var world = await SignedInAsync();
        var draftId = Guid.CreateVersion7();

        // Act
        var created = await world.Client.PostAsync(
            "/api/v1/recipes",
            new { householdId = world.HouseholdId, title = "Aubergine bake", draftId },
            Token);

        // Assert
        // The same provenance an imported recipe carries, in the same table:
        // "this did not start here" is one fact with one shape.
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
        // Arrange
        var world = await SignedInAsync();

        // Act
        var created = await world.Client.PostAsync(
            "/api/v1/recipes",
            new { householdId = world.HouseholdId, title = "Aubergine bake" },
            Token);

        var recipeId = created.Json!.Value.GetProperty("recipeId").GetGuid();
        var read = await world.Client.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        // Assert
        // A recipe written here has no origin at all, which is what makes the
        // presence of one mean something.
        Assert.False(read.Json!.Value.TryGetProperty("origin", out var origin) && origin.ValueKind is not JsonValueKind.Null);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

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
