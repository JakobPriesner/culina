using System.Net;
using Application.Abstractions.Settings;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Households;

/// <summary>
/// What one household can see of another, and what a session keeps after it
/// should have stopped.
/// </summary>
/// <remarks>
/// These are the boundaries the whole product rests on: a household is the
/// kitchen you share, and everything else is somebody else's. Documentation and
/// a dependency scanner cannot show that the boundary holds; two real accounts
/// on a real host can.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class BoundaryTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Stranger_ShouldSeeNothingOfAnotherHousehold_EvenWithItsExactIds()
    {
        // Arrange
        // Not a guessed id: the real one, which is the case a test with random
        // GUIDs never reaches. Anything that answers differently for a real id
        // than for an invented one is a way to enumerate what exists.
        var (owner, stranger) = await TwoUsersAsync();
        using var ownerClient = owner;
        using var strangerClient = stranger;

        var householdId = await FirstHouseholdIdAsync(owner);
        var recipeId = await RecipeAsync(owner, householdId);

        // Act
        var reads = new[]
        {
            await stranger.GetAsync($"/api/v1/households/{householdId}", Token),
            await stranger.GetAsync($"/api/v1/households/{householdId}/members", Token),
            await stranger.GetAsync($"/api/v1/households/{householdId}/invitations", Token),
            await stranger.GetAsync($"/api/v1/households/{householdId}/shopping-list", Token),
            await stranger.GetAsync($"/api/v1/recipes/{recipeId}", Token),
            await stranger.GetAsync($"/api/v1/recipes/{recipeId}/image", Token),
            await stranger.GetAsync($"/api/v1/recipes?householdId={householdId}", Token)
        };

        // Assert
        // 404 rather than 403 throughout: a stranger learns nothing about which
        // households or recipes exist.
        Assert.All(reads, read => Assert.Equal(HttpStatusCode.NotFound, read.StatusCode));
    }

    [Fact]
    public async Task Stranger_ShouldChangeNothingInAnotherHousehold_EvenWithItsExactIds()
    {
        // Arrange
        var (owner, stranger) = await TwoUsersAsync();
        using var ownerClient = owner;
        using var strangerClient = stranger;

        var householdId = await FirstHouseholdIdAsync(owner);
        var recipeId = await RecipeAsync(owner, householdId);

        // Act
        var writes = new[]
        {
            await stranger.PostAsync(
                $"/api/v1/households/{householdId}/invitations",
                new { },
                Token),
            await stranger.PostAsync(
                $"/api/v1/households/{householdId}/shopping-list/items",
                new { name = "Butter" },
                Token),
            await stranger.PostAsync(
                $"/api/v1/households/{householdId}/shopping-list/recipes",
                new { recipeId, servings = 4 },
                Token),
            await stranger.PostAsync(
                "/api/v1/cook-sessions",
                new { recipeId, servings = 4 },
                Token)
        };

        // Assert
        Assert.All(
            writes,
            write => Assert.True(
                write.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                $"a stranger got {(int)write.StatusCode} where they should have got nothing"));

        // And the recipe is still there, which is the part that matters.
        var stillThere = await owner.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task Delete_ShouldAnswerTheSame_ForAStrangerAndForNothingAtAll()
    {
        // Arrange
        // Deleting is idempotent: the answer says the recipe is not there any
        // more, which for a caller who could never see it was already true. The
        // subtlety is that the two answers must be the same — a 404 for a
        // recipe in somebody else's household and a 204 for one that never
        // existed would be a way to ask which recipes exist.
        var (owner, stranger) = await TwoUsersAsync();
        using var ownerClient = owner;
        using var strangerClient = stranger;

        var householdId = await FirstHouseholdIdAsync(owner);
        var recipeId = await RecipeAsync(owner, householdId);

        // Act
        var theirs = await stranger.DeleteAsync($"/api/v1/recipes/{recipeId}", Token);
        var imagined = await stranger.DeleteAsync($"/api/v1/recipes/{Guid.NewGuid()}", Token);

        // Assert
        Assert.Equal(imagined.StatusCode, theirs.StatusCode);

        // And nothing was deleted, which is the part that matters.
        var stillThere = await owner.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task Member_ShouldLoseAccess_TheMomentTheyAreRemoved()
    {
        // Arrange
        // A live session is not a licence. Membership is read on every request,
        // and somebody shown out of a household must stop seeing its recipes
        // without having to be signed out first.
        var (owner, joiner) = await TwoUsersAsync();
        using var ownerClient = owner;
        using var joinerClient = joiner;

        var householdId = await FirstHouseholdIdAsync(owner);
        var recipeId = await RecipeAsync(owner, householdId);

        var invitation = await owner.PostAsync(
            $"/api/v1/households/{householdId}/invitations",
            new { },
            Token);

        var code = invitation.Json!.Value.GetProperty("code").GetString()!;

        await joiner.PostAsync($"/api/v1/invitations/{code}/redemptions", new { }, Token);

        var whileInside = await joiner.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        // Act
        var joinerId = (await joiner.GetAsync("/api/v1/users/me", Token))
            .Json!.Value.GetProperty("userId").GetGuid();

        await owner.DeleteAsync(
            $"/api/v1/households/{householdId}/members/{joinerId}",
            Token);

        var afterwards = await joiner.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, whileInside.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);
    }

    [Fact]
    public async Task Session_ShouldStopWorking_TheMomentItIsRevokedFromAnotherDevice()
    {
        // Arrange
        // Signing out the tablet left in a holiday flat is the whole reason the
        // devices list exists. A revoked session that still works until it
        // expires is a devices list that lies.
        await postgres.ResetAsync(Token);

        using var laptop = postgres.Api.NewApiClient();
        using var tablet = postgres.Api.NewApiClient();

        await laptop.PostAsync(
            "/api/v1/users",
            new { email = "ada@example.com", displayName = "Ada", password = Password },
            Token);
        await laptop.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password },
            Token);
        await tablet.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password },
            Token);

        var sessions = await laptop.GetAsync("/api/v1/sessions", Token);
        var theTablet = sessions.Json!.Value.GetProperty("items")
            .EnumerateArray()
            .First(session => !session.GetProperty("isCurrent").GetBoolean());

        // Act
        var revoked = await laptop.DeleteAsync(
            $"/api/v1/sessions/{theTablet.GetProperty("sessionId").GetGuid()}",
            Token);

        var afterwards = await tablet.GetAsync("/api/v1/users/me", Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);

        // And the laptop that did the revoking is untouched.
        var laptopStill = await laptop.GetAsync("/api/v1/users/me", Token);

        Assert.Equal(HttpStatusCode.OK, laptopStill.StatusCode);
    }

    private static async Task<Guid> FirstHouseholdIdAsync(ApiClient client) =>
        (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();

    private static async Task<Guid> RecipeAsync(ApiClient client, Guid householdId) =>
        (await client.PostAsync(
            "/api/v1/recipes",
            new { householdId, title = "Bolognese" },
            Token)).Json!.Value.GetProperty("recipeId").GetGuid();

    private async Task<ApiClient> SignedInAsync(string email)
    {
        var client = postgres.Api.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email, displayName = "Someone", password = Password },
            Token);
        await client.PostAsync("/api/v1/sessions", new { email, password = Password }, Token);

        return client;
    }

    /// <summary>Two accounts in two different households, on one instance.</summary>
    private async Task<(ApiClient Owner, ApiClient Other)> TwoUsersAsync()
    {
        await postgres.ResetAsync(Token);

        var owner = await SignedInAsync("ada@example.com");

        var settings = postgres.Api.Services.GetRequiredService<RegistrationSettings>();

        settings.OpenRegistration = true;

        var other = await SignedInAsync("grace@example.com");

        await other.PostAsync("/api/v1/households", new { name = "Grace's kitchen" }, Token);

        return (owner, other);
    }
}
