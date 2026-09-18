using System.Net;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Recipes;

/// <summary>
/// The one hole in Culina's authentication, exercised from both sides.
/// </summary>
/// <remarks>
/// Every other read in this suite proves that a stranger gets nothing. These
/// prove the exact shape of the exception: one recipe, read only, to whoever
/// holds the token — and still nothing at all to whoever does not.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class RecipeShareTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task Share_ShouldReturnTheSameLink_WhenAskedTwice()
    {
        // Arrange
        using var client = await SignedInAsync();
        var recipeId = await CreateRecipeAsync(client);

        // Act
        var first = await client.PutAsync($"/api/v1/recipes/{recipeId}/share", new { }, Token);
        var again = await client.PutAsync($"/api/v1/recipes/{recipeId}/share", new { }, Token);

        // Assert
        // The link somebody has already sent has to survive a second tap on
        // Share; a new token each time would quietly break it.
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(
            first.Json!.Value.GetProperty("token").GetString(),
            again.Json!.Value.GetProperty("token").GetString());
    }

    [Fact]
    public async Task Share_ShouldNotBeReadable_ByAnotherHousehold()
    {
        // Arrange
        using var owner = await SignedInAsync();
        var recipeId = await CreateRecipeAsync(owner);
        await owner.PutAsync($"/api/v1/recipes/{recipeId}/share", new { }, Token);

        using var stranger = await SecondUserAsync();

        // Act
        var read = await stranger.GetAsync($"/api/v1/recipes/{recipeId}/share", Token);

        // Assert
        // The token is a key to the recipe, so someone who may not read the
        // recipe may certainly not read the key — and is told the recipe does
        // not exist, as everywhere else.
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    [Fact]
    public async Task SharedRecipe_ShouldBeReadable_WithNoSessionAtAll()
    {
        // Arrange
        using var owner = await SignedInAsync();
        var recipeId = await CreateRecipeAsync(owner);
        var token = await ShareAsync(owner, recipeId);

        using var visitor = postgres.Api.NewApiClient();

        // Act
        var read = await visitor.GetAsync($"/api/v1/shared-recipes/{token}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal("Bolognese", read.Json!.Value.GetProperty("title").GetString());
    }

    [Fact]
    public async Task SharedRecipe_ShouldCarryNothingAboutWhoseKitchenItIs()
    {
        // Arrange
        using var owner = await SignedInAsync();
        var recipeId = await CreateRecipeAsync(owner);
        var token = await ShareAsync(owner, recipeId);

        using var visitor = postgres.Api.NewApiClient();

        // Act
        var read = await visitor.GetAsync($"/api/v1/shared-recipes/{token}", Token);

        // Assert
        // The whole reason this is its own contract. A field added to
        // RecipeDetail must not appear here by inheritance.
        var body = read.Json!.Value;
        Assert.False(body.TryGetProperty("householdId", out _));
        Assert.False(body.TryGetProperty("createdBy", out _));
        Assert.False(body.TryGetProperty("version", out _));
        Assert.False(body.TryGetProperty("recipeId", out _));
    }

    [Fact]
    public async Task SharedRecipe_ShouldStopAnswering_OnceTheLinkIsTakenBack()
    {
        // Arrange
        using var owner = await SignedInAsync();
        var recipeId = await CreateRecipeAsync(owner);
        var token = await ShareAsync(owner, recipeId);

        using var visitor = postgres.Api.NewApiClient();

        // Act
        await owner.DeleteAsync($"/api/v1/recipes/{recipeId}/share", Token);
        var read = await visitor.GetAsync($"/api/v1/shared-recipes/{token}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    [Fact]
    public async Task Sharing_Again_ShouldMintANewToken_SoARevokedLinkStaysDead()
    {
        // Arrange
        using var owner = await SignedInAsync();
        var recipeId = await CreateRecipeAsync(owner);
        var first = await ShareAsync(owner, recipeId);
        await owner.DeleteAsync($"/api/v1/recipes/{recipeId}/share", Token);

        // Act
        var second = await ShareAsync(owner, recipeId);

        // Assert
        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task SharedRecipe_ShouldRefuseAnInventedToken()
    {
        // Arrange
        using var visitor = postgres.Api.NewApiClient();

        // Act
        var read = await visitor.GetAsync("/api/v1/shared-recipes/not-a-real-token", Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal("recipes.share_not_found", read.ProblemCode);
    }

    [Fact]
    public async Task UnsharedRecipe_ShouldReportNoLink_RatherThanAnEmptyOne()
    {
        // Arrange
        using var client = await SignedInAsync();
        var recipeId = await CreateRecipeAsync(client);

        // Act
        var read = await client.GetAsync($"/api/v1/recipes/{recipeId}/share", Token);

        // Assert
        // 404 is the off state the sheet renders, not a fault.
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    [Fact]
    public async Task SharedRecipe_ShouldNotBeCachedByAnythingInBetween()
    {
        // Arrange
        using var owner = await SignedInAsync();
        var recipeId = await CreateRecipeAsync(owner);
        var token = await ShareAsync(owner, recipeId);

        using var visitor = postgres.Api.NewApiClient();

        // Act
        var read = await visitor.GetAsync($"/api/v1/shared-recipes/{token}", Token);

        // Assert
        // The address carries a credential. A proxy holding on to the answer
        // would be serving a household's recipe from a store nobody can revoke.
        Assert.Contains("no-store", read.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    private static async Task<string> ShareAsync(ApiClient client, Guid recipeId)
    {
        var shared = await client.PutAsync($"/api/v1/recipes/{recipeId}/share", new { }, Token);

        return shared.Json!.Value.GetProperty("token").GetString()!;
    }

    private static async Task<Guid> CreateRecipeAsync(ApiClient client)
    {
        var householdId = (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();

        var created = await client.PostAsync(
            "/api/v1/recipes",
            new { householdId, title = "Bolognese" },
            Token);

        return created.Json!.Value.GetProperty("recipeId").GetGuid();
    }

    private async Task<ApiClient> SignedInAsync()
    {
        await postgres.ResetAsync(Token);

        return await SignInAsync("ada@example.com");
    }

    private async Task<ApiClient> SecondUserAsync()
    {
        var settings = postgres.Api.Services
            .GetRequiredService<Application.Abstractions.Settings.RegistrationSettings>();
        settings.OpenRegistration = true;
        settings.RequireInvitation = false;

        return await SignInAsync("grace@example.com");
    }

    private async Task<ApiClient> SignInAsync(string email)
    {
        var client = postgres.Api.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email, displayName = "Ada", password = Password },
            Token);
        await client.PostAsync("/api/v1/sessions", new { email, password = Password }, Token);

        return client;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
