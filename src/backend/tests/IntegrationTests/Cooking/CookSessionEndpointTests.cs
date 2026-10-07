using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Cooking;

/// <summary>At most one session per person, enforced by the database, so "what am I cooking?" has one answer even when two devices act at once.</summary>
[Collection(RequiresDatabase.Name)]
public class CookSessionEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Current_ShouldSayNothingIsCooking_WhenNothingIs()
    {
        using var client = await SignedInAsync();

        var response = await client.GetAsync("/api/v1/cook-sessions/current", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("cooking.session_not_found", response.ProblemCode);
    }

    [Fact]
    public async Task Start_ShouldReturnTheSession_WithTheRecipeTitle()
    {
        using var client = await SignedInAsync();
        var recipeId = await RecipeAsync(client, "Lemon orzo");

        var response = await client.PostAsync(
            "/api/v1/cook-sessions",
            new { recipeId, servings = 4 },
            Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        // The title travels with it, so the resume bar is one request.
        Assert.Equal("Lemon orzo", response.Json!.Value.GetProperty("recipeTitle").GetString());
        Assert.Equal(0, response.Json!.Value.GetProperty("currentStepIndex").GetInt32());
    }

    [Fact]
    public async Task Start_ShouldGiveUpTheOneAlreadyGoing()
    {
        using var client = await SignedInAsync();
        var first = await RecipeAsync(client, "Lemon orzo");
        var second = await RecipeAsync(client, "Tomato soup");

        await client.PostAsync("/api/v1/cook-sessions", new { recipeId = first, servings = 2 }, Token);

        await client.PostAsync("/api/v1/cook-sessions", new { recipeId = second, servings = 2 }, Token);

        // One answer, the newer; the partial unique index guarantees there is only one to find.
        var current = await client.GetAsync("/api/v1/cook-sessions/current", Token);

        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        Assert.Equal("Tomato soup", current.Json!.Value.GetProperty("recipeTitle").GetString());
    }

    [Fact]
    public async Task Update_ShouldMoveTheStep_WithoutBumpingTheVersion()
    {
        using var client = await SignedInAsync();
        var recipeId = await RecipeAsync(client, "Lemon orzo");
        var started = await client.PostAsync(
            "/api/v1/cook-sessions",
            new { recipeId, servings = 2 },
            Token);

        var sessionId = started.Json!.Value.GetProperty("sessionId").GetGuid();
        var version = started.Json!.Value.GetProperty("version").GetInt64();

        var moved = await client.PatchAsync(
            $"/api/v1/cook-sessions/{sessionId}",
            new { currentStepIndex = 2 },
            Token);

        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(2, moved.Json!.Value.GetProperty("currentStepIndex").GetInt32());
        // Step advances are constant; making each a concurrency event would leave a second device stale for no benefit.
        Assert.Equal(version, moved.Json!.Value.GetProperty("version").GetInt64());
    }

    [Fact]
    public async Task Update_ShouldBumpTheVersion_WhenTheScalingChanges()
    {
        using var client = await SignedInAsync();
        var recipeId = await RecipeAsync(client, "Lemon orzo");
        var started = await client.PostAsync(
            "/api/v1/cook-sessions",
            new { recipeId, servings = 2 },
            Token);

        var sessionId = started.Json!.Value.GetProperty("sessionId").GetGuid();
        var version = started.Json!.Value.GetProperty("version").GetInt64();

        var rescaled = await client.PatchAsync(
            $"/api/v1/cook-sessions/{sessionId}",
            new { servings = 6 },
            Token);

        Assert.Equal(HttpStatusCode.OK, rescaled.StatusCode);
        Assert.Equal(6, rescaled.Json!.Value.GetProperty("servings").GetDecimal());
        Assert.True(rescaled.Json!.Value.GetProperty("version").GetInt64() > version);
    }

    [Fact]
    public async Task End_ShouldLeaveNothingCooking()
    {
        using var client = await SignedInAsync();
        var recipeId = await RecipeAsync(client, "Lemon orzo");
        var started = await client.PostAsync(
            "/api/v1/cook-sessions",
            new { recipeId, servings = 2 },
            Token);

        var sessionId = started.Json!.Value.GetProperty("sessionId").GetGuid();

        var ended = await client.DeleteAsync($"/api/v1/cook-sessions/{sessionId}?completed=true", Token);

        Assert.Equal(HttpStatusCode.NoContent, ended.StatusCode);

        var current = await client.GetAsync("/api/v1/cook-sessions/current", Token);

        Assert.Equal(HttpStatusCode.NotFound, current.StatusCode);
    }

    [Fact]
    public async Task Update_ShouldNotFindSomebodyElsesSession()
    {
        using var client = await SignedInAsync();
        var recipeId = await RecipeAsync(client, "Lemon orzo");
        var started = await client.PostAsync(
            "/api/v1/cook-sessions",
            new { recipeId, servings = 2 },
            Token);

        var sessionId = started.Json!.Value.GetProperty("sessionId").GetGuid();

        using var stranger = await SecondAccountAsync();

        var response = await stranger.PatchAsync(
            $"/api/v1/cook-sessions/{sessionId}",
            new { currentStepIndex = 1 },
            Token);

        // 404, not 403: telling them it exists is telling them something.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<Guid> RecipeAsync(ApiClient client, string title)
    {
        var me = await client.GetAsync("/api/v1/users/me", Token);
        var householdId = me.Json!.Value
            .GetProperty("households")[0]
            .GetProperty("householdId")
            .GetGuid();

        var created = await client.PostAsync("/api/v1/recipes", new { householdId, title }, Token);

        return created.Json!.Value.GetProperty("recipeId").GetGuid();
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

    /// <summary>A second account, on an instance the first one opened up.</summary>
    private async Task<ApiClient> SecondAccountAsync()
    {
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
