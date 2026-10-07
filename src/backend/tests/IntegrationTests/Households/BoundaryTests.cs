using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Application.Abstractions.Settings;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Households;

/// <summary>
/// What one household can see of another, and what a session keeps after it should have stopped.
/// </summary>
/// <remarks>
/// The boundaries the whole product rests on; two real accounts on a real host can show they hold,
/// which documentation and a dependency scanner cannot.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class BoundaryTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Stranger_ShouldSeeNothingOfAnotherHousehold_EvenWithItsExactIds()
    {
        // The real id, not a guessed one: anything answering differently for a real id than an
        // invented one is a way to enumerate what exists.
        var (owner, stranger) = await TwoUsersAsync();
        using var ownerClient = owner;
        using var strangerClient = stranger;

        var (householdId, recipeId, entryId) = await PhotographedRecipeAsync(owner);

        var reads = new[]
        {
            await stranger.GetAsync($"/api/v1/households/{householdId}", Token),
            await stranger.GetAsync($"/api/v1/households/{householdId}/members", Token),
            await stranger.GetAsync($"/api/v1/households/{householdId}/invitations", Token),
            await stranger.GetAsync($"/api/v1/households/{householdId}/shopping-list", Token),
            await stranger.GetAsync($"/api/v1/households/{householdId}/ingredients?q=bol", Token),
            await stranger.GetAsync($"/api/v1/households/{householdId}/units", Token),
            await stranger.GetAsync($"/api/v1/households/{householdId}/completions?query=bol", Token),
            await stranger.GetAsync($"/api/v1/recipes/{recipeId}", Token),
            await stranger.GetAsync($"/api/v1/recipes/{recipeId}/image", Token),
            await stranger.GetAsync($"/api/v1/recipes/{recipeId}/notes", Token),
            await stranger.GetAsync($"/api/v1/recipes/{recipeId}/cook-log", Token),
            await stranger.GetAsync(CookPhoto(recipeId, entryId), Token),
            await stranger.GetAsync($"/api/v1/recipes?householdId={householdId}", Token)
        };

        // 404 rather than 403 throughout: a stranger learns nothing about which
        // households or recipes exist.
        Assert.All(reads, read => Assert.Equal(HttpStatusCode.NotFound, read.StatusCode));
    }

    [Fact]
    public async Task Stranger_ShouldChangeNothingInAnotherHousehold_EvenWithItsExactIds()
    {
        var (owner, stranger) = await TwoUsersAsync();
        using var ownerClient = owner;
        using var strangerClient = stranger;

        var householdId = await FirstHouseholdIdAsync(owner);
        var recipeId = await RecipeAsync(owner, householdId);

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

        Assert.All(
            writes,
            write => Assert.True(
                write.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
                $"a stranger got {(int)write.StatusCode} where they should have got nothing"));

        var stillThere = await owner.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task Stranger_ShouldChangeNothingInAnotherHouseholdsRecipes_EvenWithTheirExactIdsAndVersion()
    {
        // The owner's own ETag, entry and photo, and an archive that restores cleanly where the
        // stranger is allowed: nothing is refused for being malformed, only for being somebody
        // else's.
        var (owner, stranger) = await TwoUsersAsync();
        using var ownerClient = owner;
        using var strangerClient = stranger;

        var (householdId, recipeId, entryId) = await PhotographedRecipeAsync(owner);
        var before = await owner.GetAsync($"/api/v1/recipes/{recipeId}", Token);
        var strangersKitchen = await FirstHouseholdIdAsync(stranger);
        await RecipeAsync(stranger, strangersKitchen);
        var archive = (await stranger.GetAsync($"/api/v1/households/{strangersKitchen}/archive", Token)).Body;

        var writes = new[]
        {
            await PutRecipeAsync(stranger, recipeId, before.ETag!),
            await stranger.PutAsync(
                $"/api/v1/recipes/{recipeId}/notes",
                new { overall = "Mine now.", steps = Array.Empty<object>() },
                Token),
            await stranger.PostAsync($"/api/v1/recipes/{recipeId}/cook-log", new { }, Token),
            await stranger.SendAsync(PhotoUpload(recipeId, entryId), Token),
            await stranger.DeleteAsync(CookPhoto(recipeId, entryId), Token),
            await stranger.SendAsync(ArchiveUpload(householdId, archive), Token)
        };

        Assert.All(writes, write => Assert.Equal(HttpStatusCode.NotFound, write.StatusCode));

        var after = await owner.GetAsync($"/api/v1/recipes/{recipeId}", Token);
        var library = await owner.GetAsync($"/api/v1/recipes?householdId={householdId}", Token);
        var photo = await owner.GetAsync(CookPhoto(recipeId, entryId), Token);

        Assert.Equal(before.ETag, after.ETag);
        Assert.Equal("Bolognese", after.Json!.Value.GetProperty("title").GetString());
        Assert.Equal(1, library.Json!.Value.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal(1, await CountAsync($"select count(*) from cook_log_entries where recipe_id = '{recipeId}';"));
        Assert.Equal(0, await CountAsync($"select count(*) from personal_notes where recipe_id = '{recipeId}';"));
    }

    [Fact]
    public async Task Stranger_ShouldLearnNothingAboutWhoIsInAnotherHousehold_OrWhoInheritsIt()
    {
        // The real owner and heir beside invented ids: an answer that differs says who is in the
        // household, and that it exists.
        var (owner, stranger) = await TwoUsersAsync();
        using var ownerClient = owner;
        using var strangerClient = stranger;

        var householdId = await FirstHouseholdIdAsync(owner);
        var ownerId = (await owner.GetAsync("/api/v1/users/me", Token)).Json!.Value.GetProperty("userId").GetGuid();
        var heirId = (await owner.PostAsync("/api/v1/households", new { name = "Flat", inheritsFrom = householdId }, Token))
            .Json!.Value.GetProperty("householdId").GetGuid();
        var invented = Guid.NewGuid();

        var answers = new[]
        {
            await stranger.DeleteAsync($"/api/v1/households/{householdId}/members/{ownerId}", Token),
            await stranger.DeleteAsync($"/api/v1/households/{householdId}/members/{invented}", Token),
            await stranger.PatchAsync($"/api/v1/households/{householdId}/members/{ownerId}", new { role = "member" }, Token),
            await stranger.PatchAsync($"/api/v1/households/{householdId}/members/{invented}", new { role = "member" }, Token),
            await stranger.DeleteAsync($"/api/v1/households/{householdId}/heirs/{heirId}", Token),
            await stranger.DeleteAsync($"/api/v1/households/{householdId}/heirs/{invented}", Token)
        };

        Assert.All(answers, answer =>
        {
            Assert.Equal(HttpStatusCode.NotFound, answer.StatusCode);
            Assert.Equal("households.not_found", answer.ProblemCode);
        });
        Assert.Single(answers.Select(answer => answer.Json!.Value.GetProperty("detail").GetString()).Distinct());

        var members = await owner.GetAsync($"/api/v1/households/{householdId}/members", Token);
        var heirs = await owner.GetAsync($"/api/v1/households/{householdId}/heirs", Token);
        Assert.Equal("owner", members.Json!.Value.GetProperty("items")[0].GetProperty("role").GetString());
        Assert.Equal(1, heirs.Json!.Value.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Delete_ShouldAnswerTheSame_ForAStrangerAndForNothingAtAll()
    {
        // Deleting is idempotent: "not there any more" was already true for a caller who could
        // never see it. The two answers must match: a 404 for another household's recipe and a 204
        // for one that never existed would reveal which exist.
        var (owner, stranger) = await TwoUsersAsync();
        using var ownerClient = owner;
        using var strangerClient = stranger;

        var householdId = await FirstHouseholdIdAsync(owner);
        var recipeId = await RecipeAsync(owner, householdId);

        // A version a stranger could guess, so the answer is about access, not a missing
        // precondition.
        var theirs = await stranger.DeleteAsync($"/api/v1/recipes/{recipeId}", "\"v1\"", Token);
        var imagined = await stranger.DeleteAsync($"/api/v1/recipes/{Guid.NewGuid()}", "\"v1\"", Token);

        Assert.Equal(imagined.StatusCode, theirs.StatusCode);

        var stillThere = await owner.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task Member_ShouldLoseAccess_TheMomentTheyAreRemoved()
    {
        // A live session is not a licence: membership is read on every request, so somebody shown
        // out of a household stops seeing its recipes without signing out.
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

        var joinerId = (await joiner.GetAsync("/api/v1/users/me", Token))
            .Json!.Value.GetProperty("userId").GetGuid();

        await owner.DeleteAsync(
            $"/api/v1/households/{householdId}/members/{joinerId}",
            Token);

        var afterwards = await joiner.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        Assert.Equal(HttpStatusCode.OK, whileInside.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, afterwards.StatusCode);
    }

    [Fact]
    public async Task Session_ShouldStopWorking_TheMomentItIsRevokedFromAnotherDevice()
    {
        // Signing out the tablet left in a holiday flat is why the devices list exists; a revoked
        // session that works until it expires is a list that lies.
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

        var revoked = await laptop.DeleteAsync(
            $"/api/v1/sessions/{theTablet.GetProperty("sessionId").GetGuid()}",
            Token);

        var afterwards = await tablet.GetAsync("/api/v1/users/me", Token);

        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);

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

    /// <summary>
    /// The owner's recipe with a picture, and one time they cooked it with a photo: every id a
    /// stranger could aim at, all of them real.
    /// </summary>
    private static async Task<(Guid HouseholdId, Guid RecipeId, Guid EntryId)> PhotographedRecipeAsync(ApiClient owner)
    {
        var householdId = await FirstHouseholdIdAsync(owner);
        var recipeId = await RecipeAsync(owner, householdId);
        await new Kitchen(owner, householdId).PictureAsync(recipeId, TestImages.Png(2, 2));

        var entryId = (await owner.PostAsync($"/api/v1/recipes/{recipeId}/cook-log", new { }, Token))
            .Json!.Value.GetProperty("entryId").GetGuid();
        var photographed = await owner.SendAsync(PhotoUpload(recipeId, entryId), Token);

        Assert.Equal(HttpStatusCode.OK, photographed.StatusCode);

        return (householdId, recipeId, entryId);
    }

    private static string CookPhoto(Guid recipeId, Guid entryId) =>
        $"/api/v1/recipes/{recipeId}/cook-log/{entryId}/photo";

    private static HttpRequestMessage PhotoUpload(Guid recipeId, Guid entryId) =>
        Upload(HttpMethod.Put, CookPhoto(recipeId, entryId), TestImages.Png(3, 3), "image/png", "attempt.png");

    private static HttpRequestMessage ArchiveUpload(Guid householdId, string archive) =>
        Upload(
            HttpMethod.Post,
            $"/api/v1/households/{householdId}/archive",
            Encoding.UTF8.GetBytes(archive),
            "application/json",
            "culina.json");

    private static HttpRequestMessage Upload(HttpMethod method, string path, byte[] bytes, string type, string name)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(type);

        return new HttpRequestMessage(method, path) { Content = new MultipartFormDataContent { { file, "file", name } } };
    }

    /// <summary>
    /// Saves the whole recipe under a new title, as the editor would, with the version given.
    /// </summary>
    private static Task<ApiResponse> PutRecipeAsync(ApiClient client, Guid recipeId, string etag)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{recipeId}")
        {
            Content = JsonContent.Create(new
            {
                title = "Somebody else's now",
                language = "en",
                yieldAmount = 4,
                yieldKind = "servings",
                groups = Array.Empty<object>(),
                steps = Array.Empty<object>(),
                tags = Array.Empty<string>()
            })
        };

        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(etag));

        return client.SendAsync(request, Token);
    }

    private async Task<int> CountAsync(string sql) =>
        (int)await postgres.QuerySingleAsync<long>(sql, Token);

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
        settings.RequireInvitation = false;

        var other = await SignedInAsync("grace@example.com");

        await other.PostAsync("/api/v1/households", new { name = "Grace's kitchen" }, Token);

        return (owner, other);
    }
}
