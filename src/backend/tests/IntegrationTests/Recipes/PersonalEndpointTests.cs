using System.Net;
using Application.Abstractions.Settings;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Recipes;

/// <summary>
/// Notes and cooking history are person-owned. That separation is the reason
/// two people in one household can disagree about a recipe without either of
/// them editing it, so it is tested at the HTTP level too.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class PersonalEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task Notes_ShouldStartEmpty_AndRoundTripWhatIsWritten()
    {
        // Arrange
        using var client = await SignedInAsync("ada@example.com");
        var recipeId = await CreateRecipeAsync(client);

        // Act
        var before = await client.GetAsync($"/api/v1/recipes/{recipeId}/notes", Token);
        await client.PutAsync(
            $"/api/v1/recipes/{recipeId}/notes",
            new { overall = "I use half the sugar.", steps = Array.Empty<object>() },
            Token);
        var after = await client.GetAsync($"/api/v1/recipes/{recipeId}/notes", Token);

        // Assert
        Assert.Equal(JsonValueKindNull, before.Json!.Value.GetProperty("overall").ValueKind);
        Assert.Equal("I use half the sugar.", after.Json!.Value.GetProperty("overall").GetString());
    }

    [Fact]
    public async Task Notes_ShouldBeDeletedByAnEmptyBody_RatherThanStoredBlank()
    {
        // Arrange
        using var client = await SignedInAsync("ada@example.com");
        var recipeId = await CreateRecipeAsync(client);
        await client.PutAsync(
            $"/api/v1/recipes/{recipeId}/notes",
            new { overall = "Something.", steps = Array.Empty<object>() },
            Token);

        // Act
        await client.PutAsync(
            $"/api/v1/recipes/{recipeId}/notes",
            new { overall = "   ", steps = Array.Empty<object>() },
            Token);
        var after = await client.GetAsync($"/api/v1/recipes/{recipeId}/notes", Token);

        // Assert
        // A stored blank would make the panel show an empty box nobody asked for.
        Assert.Equal(JsonValueKindNull, after.Json!.Value.GetProperty("overall").ValueKind);
    }

    [Fact]
    public async Task Notes_ShouldBeInvisibleToAnotherMemberOfTheSameHousehold()
    {
        // Arrange
        var (owner, housemate) = await TwoInOneHouseholdAsync();
        using var ownerClient = owner;
        using var housemateClient = housemate;
        var recipeId = await CreateRecipeAsync(owner);
        await owner.PutAsync(
            $"/api/v1/recipes/{recipeId}/notes",
            new { overall = "Mine alone.", steps = Array.Empty<object>() },
            Token);

        // Act
        var theirs = await housemate.GetAsync($"/api/v1/recipes/{recipeId}/notes", Token);

        // Assert
        // The recipe is shared; the note is not.
        Assert.Equal(HttpStatusCode.OK, theirs.StatusCode);
        Assert.Equal(JsonValueKindNull, theirs.Json!.Value.GetProperty("overall").ValueKind);
    }

    [Fact]
    public async Task CookLog_ShouldRecordWithAnEmptyBody_BecauseOneTapIsTheWholeInteraction()
    {
        // Arrange
        using var client = await SignedInAsync("ada@example.com");
        var recipeId = await CreateRecipeAsync(client);

        // Act
        var recorded = await client.PostAsync($"/api/v1/recipes/{recipeId}/cook-log", new { }, Token);

        // Assert
        Assert.Equal(HttpStatusCode.Created, recorded.StatusCode);
        Assert.Equal(1, recorded.Json!.Value.GetProperty("count").GetInt32());
        Assert.NotEqual(Guid.Empty, recorded.Json!.Value.GetProperty("entryId").GetGuid());
    }

    [Fact]
    public async Task CookLog_ShouldCountUp_AndReportWhenYouLastMadeIt()
    {
        // Arrange
        using var client = await SignedInAsync("ada@example.com");
        var recipeId = await CreateRecipeAsync(client);

        // Act
        await client.PostAsync($"/api/v1/recipes/{recipeId}/cook-log", new { }, Token);
        await client.PostAsync($"/api/v1/recipes/{recipeId}/cook-log", new { }, Token);
        var log = await client.GetAsync($"/api/v1/recipes/{recipeId}/cook-log", Token);

        // Assert
        // "You've made this twice" is the whole feature, and it is why Culina
        // has no star ratings.
        Assert.Equal(2, log.Json!.Value.GetProperty("count").GetInt32());
        Assert.NotEqual(JsonValueKindNull, log.Json!.Value.GetProperty("lastMadeAt").ValueKind);
    }

    [Fact]
    public async Task CookLog_ShouldBeSeparatePerPerson_InTheSameHousehold()
    {
        // Arrange
        var (owner, housemate) = await TwoInOneHouseholdAsync();
        using var ownerClient = owner;
        using var housemateClient = housemate;
        var recipeId = await CreateRecipeAsync(owner);
        await owner.PostAsync($"/api/v1/recipes/{recipeId}/cook-log", new { }, Token);

        // Act
        var theirs = await housemate.GetAsync($"/api/v1/recipes/{recipeId}/cook-log", Token);

        // Assert
        Assert.Equal(0, theirs.Json!.Value.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Notes_ShouldRejectAStepFromADifferentRecipe()
    {
        // Arrange
        using var client = await SignedInAsync("ada@example.com");
        var recipeId = await CreateRecipeAsync(client);

        // Act
        var response = await client.PutAsync(
            $"/api/v1/recipes/{recipeId}/notes",
            new
            {
                overall = (string?)null,
                steps = new[] { new { stepId = Guid.CreateVersion7(), body = "Careful here." } }
            },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("cooking.unknown_step", response.ProblemCode);
    }

    private const System.Text.Json.JsonValueKind JsonValueKindNull = System.Text.Json.JsonValueKind.Null;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<Guid> CreateRecipeAsync(ApiClient client)
    {
        var householdId = (await client.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();

        return (await client.PostAsync(
                "/api/v1/recipes",
                new { householdId, title = "Bolognese" },
                Token))
            .Json!.Value.GetProperty("recipeId").GetGuid();
    }

    private async Task<ApiClient> SignedInAsync(string email)
    {
        await postgres.ResetAsync(Token);

        return await SignInAsync(email);
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

    private async Task<(ApiClient Owner, ApiClient Housemate)> TwoInOneHouseholdAsync()
    {
        var owner = await SignedInAsync("ada@example.com");

        var settings = postgres.Api.Services.GetRequiredService<RegistrationSettings>();
        settings.OpenRegistration = true;
        settings.RequireInvitation = true;

        var householdId = (await owner.GetAsync("/api/v1/households", Token))
            .Json!.Value.GetProperty("items")[0].GetProperty("householdId").GetGuid();
        var code = (await owner.PostAsync($"/api/v1/households/{householdId}/invitations", new { }, Token))
            .Json!.Value.GetProperty("code").GetString();

        var housemate = postgres.Api.NewApiClient();
        await housemate.PostAsync(
            "/api/v1/users",
            new
            {
                email = "grace@example.com",
                displayName = "Grace",
                password = Password,
                invitationCode = code
            },
            Token);
        await housemate.PostAsync(
            "/api/v1/sessions",
            new { email = "grace@example.com", password = Password },
            Token);

        return (owner, housemate);
    }
}
