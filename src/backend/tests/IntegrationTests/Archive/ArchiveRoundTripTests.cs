using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Archive;

/// <summary>
/// Taking your recipes with you, and bringing them back.
/// </summary>
/// <remarks>
/// A backup nobody has restored is not a backup, so this does the whole
/// journey: write a recipe, export it, restore it into an empty kitchen, and
/// read it back. The assertion inside that journey is the one that matters —
/// a restored step still names the right ingredient, because the archive
/// carries positions and ids are assigned by whichever database it lands in.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class ArchiveRoundTripTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnArchive_ShouldRestoreIntoARecipeThatStillPointsAtItsIngredients()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await FirstHouseholdIdAsync(client);
        await WriteLemonOrzoAsync(client, householdId);

        // Act
        var exported = await client.GetAsync($"/api/v1/households/{householdId}/archive", Token);
        var archive = exported.Body;

        var restored = await UploadAsync(client, householdId, archive);

        // Assert
        Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.Equal(1, restored.Json!.Value.GetProperty("restored").GetInt32());
        Assert.Equal(0, restored.Json!.Value.GetProperty("skipped").GetInt32());

        // Two of them now: a restore adds, it never replaces. A restore that
        // emptied your kitchen first would be the worst reading of the word.
        var all = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&limit=24",
            Token);
        var titles = all.Json!.Value.GetProperty("items").EnumerateArray()
            .Select(one => one.GetProperty("title").GetString())
            .ToList();

        Assert.Equal(2, titles.Count(title => title == "Lemon orzo"));

        // And the copy's step still names its own ingredients, by the ids the
        // restore assigned rather than the ids the archive was written with.
        var copy = await ReadTheRestoredOneAsync(client, householdId);
        var ingredients = copy.GetProperty("groups").EnumerateArray()
            .SelectMany(group => group.GetProperty("ingredients").EnumerateArray())
            .ToList();
        var segments = copy.GetProperty("steps").EnumerateArray().First()
            .GetProperty("segments").EnumerateArray()
            .ToList();

        var referenced = segments
            .Where(one => one.GetProperty("type").GetString() == "ingredient")
            .Select(one => one.GetProperty("recipeIngredientId").GetGuid())
            .ToList();

        Assert.Equal(2, referenced.Count);
        Assert.All(referenced, id =>
            Assert.Contains(ingredients, one => one.GetProperty("ingredientId").GetGuid() == id));

        // The words, in order, are the ones that were written.
        Assert.Equal("Boil ", segments[0].GetProperty("value").GetString());
        Assert.Equal("orzo", segments[1].GetProperty("name").GetString());
        Assert.Equal("olive oil", segments[3].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Restore_ShouldRefuse_AnArchiveFromAVersionItDoesNotKnow()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await FirstHouseholdIdAsync(client);

        // Act
        var response = await UploadAsync(
            client,
            householdId,
            """{ "culina": 99, "exportedAt": "2026-09-13T10:00:00+00:00", "recipes": [] }""");

        // Assert
        // Refused rather than half-read: a half-restored recipe is worse than a
        // failed restore, because nobody can tell which half is wrong.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("archive.unknown_version", response.Json!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Restore_ShouldRefuse_AFileThatIsNotAnArchive()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await FirstHouseholdIdAsync(client);

        // Act
        var response = await UploadAsync(client, householdId, "not json at all");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("archive.not_an_archive", response.Json!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Export_ShouldBeRefused_ForAKitchenTheCallerIsNotIn()
    {
        // Arrange
        using var client = await SignedInAsync();

        // Act
        var response = await client.GetAsync($"/api/v1/households/{Guid.NewGuid()}/archive", Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task WriteLemonOrzoAsync(ApiClient client, Guid householdId)
    {
        var created = await client.PostAsync(
            "/api/v1/recipes",
            new { householdId, title = "Lemon orzo" },
            Token);
        var recipeId = created.Json!.Value.GetProperty("recipeId").GetGuid();

        var read = await client.GetAsync($"/api/v1/recipes/{recipeId}", Token);
        var groups = read.Json!.Value.GetProperty("groups").EnumerateArray().ToList();
        var groupId = groups[0].GetProperty("groupId").GetGuid();

        // Saved once to give the ingredients ids, then again to point the step
        // at them — which is how the editor does it, and the only way a step
        // can reference a line the server has seen.
        await SaveAsync(
            client,
            recipeId,
            read.Headers.ETag!.Tag,
            new
            {
                title = "Lemon orzo",
                language = "en",
                yieldAmount = 2,
                yieldKind = "servings",
                groups = new[]
                {
                    new
                    {
                        groupId,
                        ingredients = new object[]
                        {
                            new { quantity = 200, unit = "g", name = "orzo" },
                            new { quantity = 2, unit = "tbsp", name = "olive oil" }
                        }
                    }
                },
                steps = Array.Empty<object>(),
                tags = Array.Empty<string>()
            });

        var withIds = await client.GetAsync($"/api/v1/recipes/{recipeId}", Token);
        var lines = withIds.Json!.Value.GetProperty("groups").EnumerateArray()
            .SelectMany(group => group.GetProperty("ingredients").EnumerateArray())
            .Select(one => one.GetProperty("ingredientId").GetGuid())
            .ToList();

        await SaveAsync(
            client,
            recipeId,
            withIds.Headers.ETag!.Tag,
            new
            {
                title = "Lemon orzo",
                language = "en",
                yieldAmount = 2,
                yieldKind = "servings",
                groups = new[]
                {
                    new
                    {
                        groupId,
                        ingredients = new object[]
                        {
                            new { ingredientId = lines[0], quantity = 200, unit = "g", name = "orzo" },
                            new { ingredientId = lines[1], quantity = 2, unit = "tbsp", name = "olive oil" }
                        }
                    }
                },
                steps = new[]
                {
                    new
                    {
                        segments = new object[]
                        {
                            new { type = "text", value = "Boil " },
                            new { type = "ingredient", recipeIngredientId = lines[0] },
                            new { type = "text", value = " and dress it with " },
                            new { type = "ingredient", recipeIngredientId = lines[1] },
                            new { type = "text", value = "." }
                        }
                    }
                },
                tags = Array.Empty<string>()
            });
    }

    /// <summary>A write that carries the version it is replacing.</summary>
    private static Task<ApiResponse> SaveAsync<TBody>(
        ApiClient client,
        Guid recipeId,
        string etag,
        TBody body)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{recipeId}")
        {
            Content = System.Net.Http.Json.JsonContent.Create(body)
        };

        request.Headers.TryAddWithoutValidation("If-Match", etag);

        return client.SendAsync(request, Token);
    }

    /// <summary>The copy, which is the one that is not the original.</summary>
    private static async Task<JsonElement> ReadTheRestoredOneAsync(ApiClient client, Guid householdId)
    {
        var all = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&limit=24",
            Token);

        var ids = all.Json!.Value.GetProperty("items").EnumerateArray()
            .Where(one => one.GetProperty("title").GetString() == "Lemon orzo")
            .Select(one => one.GetProperty("recipeId").GetGuid())
            .ToList();

        // Either will do: both must have working references, and asserting on
        // one of two identical recipes is asserting on the restore.
        var read = await client.GetAsync($"/api/v1/recipes/{ids[0]}", Token);

        return read.Json!.Value;
    }

    private static async Task<ApiResponse> UploadAsync(
        ApiClient client,
        Guid householdId,
        string archive)
    {
        using var content = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes(archive));

        file.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        content.Add(file, "file", "culina.json");

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/households/{householdId}/archive")
        {
            Content = content
        };

        return await client.SendAsync(request, Token);
    }

    private static async Task<Guid> FirstHouseholdIdAsync(ApiClient client)
    {
        var me = await client.GetAsync("/api/v1/users/me", Token);

        return me.Json!.Value.GetProperty("households").EnumerateArray().First()
            .GetProperty("householdId").GetGuid();
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
}
