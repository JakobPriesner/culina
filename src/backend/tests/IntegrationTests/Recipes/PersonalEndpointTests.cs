using System.Net;
using System.Net.Http.Json;
using Application.Abstractions.Settings;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Recipes;

/// <summary>
/// Notes and cooking history are person-owned, so two people in one household can disagree about a
/// recipe without editing it; tested at the HTTP level too.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class PersonalEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task Notes_ShouldStartEmpty_AndRoundTripWhatIsWritten()
    {
        using var client = await SignedInAsync("ada@example.com");
        var recipeId = await CreateRecipeAsync(client);

        var before = await client.GetAsync($"/api/v1/recipes/{recipeId}/notes", Token);
        await client.PutAsync(
            $"/api/v1/recipes/{recipeId}/notes",
            new { overall = "I use half the sugar.", steps = Array.Empty<object>() },
            Token);
        var after = await client.GetAsync($"/api/v1/recipes/{recipeId}/notes", Token);

        Assert.Equal(JsonValueKindNull, before.Json!.Value.GetProperty("overall").ValueKind);
        Assert.Equal("I use half the sugar.", after.Json!.Value.GetProperty("overall").GetString());
    }

    [Fact]
    public async Task Notes_ShouldBeDeletedByAnEmptyBody_RatherThanStoredBlank()
    {
        using var client = await SignedInAsync("ada@example.com");
        var recipeId = await CreateRecipeAsync(client);
        await client.PutAsync(
            $"/api/v1/recipes/{recipeId}/notes",
            new { overall = "Something.", steps = Array.Empty<object>() },
            Token);

        await client.PutAsync(
            $"/api/v1/recipes/{recipeId}/notes",
            new { overall = "   ", steps = Array.Empty<object>() },
            Token);
        var after = await client.GetAsync($"/api/v1/recipes/{recipeId}/notes", Token);

        // A stored blank would make the panel show an empty box nobody asked for.
        Assert.Equal(JsonValueKindNull, after.Json!.Value.GetProperty("overall").ValueKind);
    }

    [Fact]
    public async Task Notes_ShouldBeInvisibleToAnotherMemberOfTheSameHousehold()
    {
        var (owner, housemate) = await TwoInOneHouseholdAsync();
        using var ownerClient = owner;
        using var housemateClient = housemate;
        var recipeId = await CreateRecipeAsync(owner);
        await owner.PutAsync(
            $"/api/v1/recipes/{recipeId}/notes",
            new { overall = "Mine alone.", steps = Array.Empty<object>() },
            Token);

        var theirs = await housemate.GetAsync($"/api/v1/recipes/{recipeId}/notes", Token);

        // The recipe is shared; the note is not.
        Assert.Equal(HttpStatusCode.OK, theirs.StatusCode);
        Assert.Equal(JsonValueKindNull, theirs.Json!.Value.GetProperty("overall").ValueKind);
    }

    [Fact]
    public async Task Notes_ShouldSurviveAnotherMemberSavingTheRecipe_WhenTheStepIsKept()
    {
        var (owner, housemate) = await TwoInOneHouseholdAsync();
        using var ownerClient = owner;
        using var housemateClient = housemate;
        var recipeId = await CreateRecipeAsync(owner);
        var withStep = await SaveRecipeAsync(owner, recipeId, stepId: null);
        var stepId = withStep.Json!.Value.GetProperty("steps")[0].GetProperty("stepId").GetGuid();
        await housemate.PutAsync(
            $"/api/v1/recipes/{recipeId}/notes",
            new { overall = (string?)null, steps = new[] { new { stepId, body = "Lower the heat." } } },
            Token);

        // The editor saves on every pause in typing, so this is what fixing a
        // typo in the title does.
        var saved = await SaveRecipeAsync(owner, recipeId, stepId);
        var notes = await housemate.GetAsync($"/api/v1/recipes/{recipeId}/notes", Token);

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var step = Assert.Single(notes.Json!.Value.GetProperty("steps").EnumerateArray());
        Assert.Equal(stepId, step.GetProperty("stepId").GetGuid());
        Assert.Equal("Lower the heat.", step.GetProperty("body").GetString());
    }

    [Fact]
    public async Task CookLog_ShouldRecordWithAnEmptyBody_BecauseOneTapIsTheWholeInteraction()
    {
        using var client = await SignedInAsync("ada@example.com");
        var recipeId = await CreateRecipeAsync(client);

        var recorded = await client.PostAsync($"/api/v1/recipes/{recipeId}/cook-log", new { }, Token);

        Assert.Equal(HttpStatusCode.Created, recorded.StatusCode);
        Assert.Equal(1, recorded.Json!.Value.GetProperty("count").GetInt32());
        Assert.NotEqual(Guid.Empty, recorded.Json!.Value.GetProperty("entryId").GetGuid());
    }

    [Fact]
    public async Task CookLog_ShouldCountUp_AndReportWhenYouLastMadeIt()
    {
        using var client = await SignedInAsync("ada@example.com");
        var recipeId = await CreateRecipeAsync(client);

        await client.PostAsync($"/api/v1/recipes/{recipeId}/cook-log", new { }, Token);
        await client.PostAsync($"/api/v1/recipes/{recipeId}/cook-log", new { }, Token);
        var log = await client.GetAsync($"/api/v1/recipes/{recipeId}/cook-log", Token);

        // "You've made this twice" is the whole feature, and it is why Culina
        // has no star ratings.
        Assert.Equal(2, log.Json!.Value.GetProperty("count").GetInt32());
        Assert.NotEqual(JsonValueKindNull, log.Json!.Value.GetProperty("lastMadeAt").ValueKind);
    }

    [Fact]
    public async Task CookLog_ShouldBeSeparatePerPerson_InTheSameHousehold()
    {
        var (owner, housemate) = await TwoInOneHouseholdAsync();
        using var ownerClient = owner;
        using var housemateClient = housemate;
        var recipeId = await CreateRecipeAsync(owner);
        await owner.PostAsync($"/api/v1/recipes/{recipeId}/cook-log", new { }, Token);

        var theirs = await housemate.GetAsync($"/api/v1/recipes/{recipeId}/cook-log", Token);

        Assert.Equal(0, theirs.Json!.Value.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Notes_ShouldRejectAStepFromADifferentRecipe()
    {
        using var client = await SignedInAsync("ada@example.com");
        var recipeId = await CreateRecipeAsync(client);

        var response = await client.PutAsync(
            $"/api/v1/recipes/{recipeId}/notes",
            new
            {
                overall = (string?)null,
                steps = new[] { new { stepId = Guid.CreateVersion7(), body = "Careful here." } }
            },
            Token);

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

    /// <summary>Saves the recipe as the editor does, with one step.</summary>
    private static async Task<ApiResponse> SaveRecipeAsync(ApiClient client, Guid recipeId, Guid? stepId)
    {
        var current = await client.GetAsync($"/api/v1/recipes/{recipeId}", Token);
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{recipeId}")
        {
            Content = JsonContent.Create(new
            {
                title = "Bolognese",
                language = "en",
                yieldAmount = 4,
                yieldKind = "servings",
                groups = Array.Empty<object>(),
                steps = new[] { new { stepId, segments = new[] { new { type = "text", value = "Simmer." } } } },
                tags = Array.Empty<string>()
            })
        };

        request.Headers.TryAddWithoutValidation("If-Match", current.ETag);

        return await client.SendAsync(request, Token);
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
