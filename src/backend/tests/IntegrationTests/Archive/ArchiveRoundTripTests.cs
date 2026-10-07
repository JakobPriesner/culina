using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Archive;

/// <summary>Export, restore into an empty kitchen and read back: a restored step must still name the right ingredient (the archive carries positions, not ids).</summary>
[Collection(RequiresDatabase.Name)]
public class ArchiveRoundTripTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnArchive_ShouldRestoreIntoARecipeThatStillPointsAtItsIngredients()
    {
        using var client = await SignedInAsync();
        var householdId = await FirstHouseholdIdAsync(client);
        await WriteLemonOrzoAsync(client, householdId);

        var exported = await client.GetAsync($"/api/v1/households/{householdId}/archive", Token);
        var archive = exported.Body;

        var restored = await UploadAsync(client, householdId, archive);

        Assert.Equal(HttpStatusCode.OK, exported.StatusCode);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.Equal(1, restored.Json!.Value.GetProperty("restored").GetInt32());
        Assert.Equal(0, restored.Json!.Value.GetProperty("skipped").GetInt32());

        // A restore adds, it never replaces.
        var all = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&limit=24",
            Token);
        var titles = all.Json!.Value.GetProperty("items").EnumerateArray()
            .Select(one => one.GetProperty("title").GetString())
            .ToList();

        Assert.Equal(2, titles.Count(title => title == "Lemon orzo"));

        // The copy's steps reference the ids the restore assigned, not the archive's.
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

        Assert.Equal("Boil ", segments[0].GetProperty("value").GetString());
        Assert.Equal("orzo", segments[1].GetProperty("name").GetString());
        Assert.Equal("olive oil", segments[3].GetProperty("name").GetString());

        // Also what a step needs but never names.
        var seasoning = copy.GetProperty("steps").EnumerateArray().Last();
        var needed = Assert.Single(seasoning.GetProperty("uses").EnumerateArray()).GetGuid();
        var oil = ingredients.Single(one => one.GetProperty("name").GetString() == "olive oil");

        Assert.Equal(oil.GetProperty("ingredientId").GetGuid(), needed);

        Assert.Equal("bowls", copy.GetProperty("yieldLabel").GetString());
        Assert.Equal(
            "Boil the orzo",
            copy.GetProperty("steps").EnumerateArray().First().GetProperty("title").GetString());
    }

    [Fact]
    public async Task Restore_ShouldRefuse_AnArchiveFromAVersionItDoesNotKnow()
    {
        using var client = await SignedInAsync();
        var householdId = await FirstHouseholdIdAsync(client);

        var response = await UploadAsync(
            client,
            householdId,
            """{ "culina": 99, "exportedAt": "2026-09-13T10:00:00+00:00", "recipes": [] }""");

        // Refused rather than half-read.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("archive.unknown_version", response.Json!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Restore_ShouldRefuse_AnArchiveWithMoreRecipesThanOneRestoreWrites()
    {
        using var client = await SignedInAsync();
        var householdId = await FirstHouseholdIdAsync(client);
        var recipes = string.Join(",", Enumerable.Repeat(
            """{ "title": "T", "language": "en", "yieldAmount": 1, "yieldKind": "servings", "tags": [], "groups": [], "steps": [], "cooked": [] }""",
            2_001));

        var response = await UploadAsync(
            client,
            householdId,
            $$"""{ "culina": 1, "exportedAt": "2026-09-13T10:00:00+00:00", "recipes": [{{recipes}}] }""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("archive.too_many_recipes", response.Json!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Restore_ShouldRefuse_AFileThatIsNotAnArchive()
    {
        using var client = await SignedInAsync();
        var householdId = await FirstHouseholdIdAsync(client);

        var response = await UploadAsync(client, householdId, "not json at all");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("archive.not_an_archive", response.Json!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Export_ShouldBeRefused_ForAKitchenTheCallerIsNotIn()
    {
        // A real household: checking only a nonexistent one proves nothing.
        using var ada = await SignedInAsync();
        var householdId = await FirstHouseholdIdAsync(ada);
        await WriteLemonOrzoAsync(ada, householdId);
        using var stranger = await Kitchen.StrangerAsync(postgres);

        var response = await stranger.GetAsync($"/api/v1/households/{householdId}/archive", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("Lemon orzo", response.Body, StringComparison.Ordinal);
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

        // Saved once to give the ingredients ids, then again to point the step at them, as the editor does.
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
                yieldLabel = "bowls",
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
                steps = new object[]
                {
                    new
                    {
                        title = "Boil the orzo",
                        segments = new object[]
                        {
                            new { type = "text", value = "Boil " },
                            new { type = "ingredient", recipeIngredientId = lines[0] },
                            new { type = "text", value = " and dress it with " },
                            new { type = "ingredient", recipeIngredientId = lines[1] },
                            new { type = "text", value = "." }
                        }
                    },
                    new
                    {
                        // Needs the oil without naming it: the kind of step an archive used to lose.
                        segments = new object[]
                        {
                            new { type = "text", value = "Season and serve." }
                        },
                        uses = new[] { lines[1] }
                    }
                },
                tags = Array.Empty<string>()
            });
    }

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

    private static async Task<JsonElement> ReadTheRestoredOneAsync(ApiClient client, Guid householdId)
    {
        var all = await client.GetAsync(
            $"/api/v1/recipes?householdId={householdId}&limit=24",
            Token);

        var ids = all.Json!.Value.GetProperty("items").EnumerateArray()
            .Where(one => one.GetProperty("title").GetString() == "Lemon orzo")
            .Select(one => one.GetProperty("recipeId").GetGuid())
            .ToList();

        // Either will do: both must have working references.
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
