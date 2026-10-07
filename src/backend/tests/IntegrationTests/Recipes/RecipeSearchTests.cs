using System.Buffers.Text;
using System.Net;
using System.Text;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Recipes;

[Collection(RequiresDatabase.Name)]
public class RecipeSearchTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task Search_ShouldWrapTheResults_WithACountAndACursorSlot()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var response = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}",
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, response.Json!.Value.GetProperty("total").GetInt32());
        Assert.Equal(3, response.Json!.Value.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Search_ShouldFindByIngredientName_NotJustByTitle()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var response = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&query=ricotta",
            Token);

        // Assert
        var titles = Titles(response);
        Assert.Equal(["Lasagne"], titles);
    }

    [Fact]
    public async Task Search_ShouldFilterByTime_BecauseThatIsTheRealConstraint()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var response = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&maxMinutes=25",
            Token);

        // Assert
        // Bolognese has no stated time, so it is excluded rather than counted as zero.
        Assert.Equal(["Omelette"], Titles(response));
    }

    [Fact]
    public async Task Search_ShouldRequireEveryTag_WhenSeveralAreGiven()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var both = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&tag=quick&tag=vegetarian",
            Token);
        var one = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&tag=vegetarian",
            Token);

        // Assert
        Assert.Equal(["Omelette"], Titles(both));
        Assert.Equal(2, one.Json!.Value.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Search_ShouldAskForATagOnce_WhenItIsGivenTwice()
    {
        // Arrange
        // A repeated tag must not become an unsatisfiable requirement.
        var world = await SeedAsync();

        // Act
        var twice = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&tag=vegetarian&tag=vegetarian",
            Token);
        var once = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&tag=vegetarian",
            Token);

        // Assert
        Assert.Equal(Titles(once), Titles(twice));
        Assert.NotEmpty(Titles(twice));
    }

    [Fact]
    public async Task Search_ShouldRankByWhatYouHave_AndSayHowMuchIsMissing()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var response = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&ingredient=ricotta&ingredient=basil&sort=relevance",
            Token);

        // Assert
        var first = response.Json!.Value.GetProperty("items")[0];
        Assert.Equal("Lasagne", first.GetProperty("title").GetString());
        var match = first.GetProperty("ingredientMatch");
        Assert.Equal(2, match.GetProperty("matched").GetInt32());
        Assert.Equal(2, match.GetProperty("requested").GetInt32());
        Assert.Equal(1, match.GetProperty("missing").GetInt32());
    }

    [Fact]
    public async Task Search_ShouldOmitTheMatch_WhenNoIngredientsWereNamed()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var response = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}",
            Token);

        // Assert
        var first = response.Json!.Value.GetProperty("items")[0];
        Assert.Equal(System.Text.Json.JsonValueKind.Null, first.GetProperty("ingredientMatch").ValueKind);
    }

    [Fact]
    public async Task Search_ShouldPageWithoutRepeatingOrSkipping_WhenAskedOneAtATime()
    {
        // Arrange
        var world = await SeedAsync();
        List<string> seen = [];
        string? cursor = null;

        // Act
        do
        {
            var page = await world.Client.GetAsync(
                $"/api/v1/recipes?householdId={world.HouseholdId}&sort=title&limit=1"
                + (cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}"),
                Token);

            seen.AddRange(Titles(page));
            cursor = page.Json!.Value.GetProperty("nextCursor").GetString();
        }
        while (cursor is not null);

        // Assert
        Assert.Equal(["Bolognese", "Lasagne", "Omelette"], seen);
    }

    [Theory]
    [InlineData("-cookCount", """{"Sort":3,"Keys":["x"]}""")]
    [InlineData("-updatedAt", """{"Keys":null}""")]
    [InlineData("-updatedAt", """{"Sort":0,"Keys":["not a time"]}""")]
    [InlineData("title", """{"Sort":1,"Keys":[null]}""")]
    [InlineData("totalMinutes", """{"Sort":2,"Keys":["soon"]}""")]
    [InlineData("relevance", """{"Sort":4,"Keys":["1"]}""")]
    [InlineData("relevance", """{"Sort":4,"Keys":["-2147483648","0.5","2026-10-01T00:00:00Z"]}""")]
    [InlineData("suggested", """{"Sort":6,"Keys":["lots"]}""")]
    public async Task Search_ShouldStartAtTheTop_WhenACursorsKeysAreNotWhatItsSortWrites(
        string sort,
        string forged)
    {
        // Arrange
        // Cursors are forgeable and their keys are cast in SQL; mismatched keys are treated as a malformed cursor.
        var world = await SeedAsync();
        var path = $"/api/v1/recipes?householdId={world.HouseholdId}&sort={Uri.EscapeDataString(sort)}";
        var cursor = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(forged));

        // Act
        var first = await world.Client.GetAsync(path, Token);
        var response = await world.Client.GetAsync($"{path}&cursor={cursor}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Titles(first), Titles(response));
    }

    [Fact]
    public async Task Search_ShouldResume_WhenACursorScoreIsTooSmallForPostgreSqlToRead()
    {
        // Arrange
        // A double that underflows to zero is "out of range" to PostgreSQL, so it is written back as zero.
        var world = await SeedAsync();
        var cursor = Base64Url.EncodeToString(
            Encoding.UTF8.GetBytes("""{"Sort":4,"Keys":["0","1e-400","2026-10-01T00:00:00Z"]}"""));

        // Act
        var response = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&sort=relevance&cursor={cursor}",
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Search_ShouldSortAlphabetically_WhenAskedTo()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var response = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&sort=title",
            Token);

        // Assert
        Assert.Equal(["Bolognese", "Lasagne", "Omelette"], Titles(response));
    }

    [Fact]
    public async Task Search_ShouldPutRecipesWithNoStatedTimeLast_WhenSortingByTime()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var response = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&sort=totalMinutes",
            Token);

        // Assert
        Assert.Equal(["Omelette", "Lasagne", "Bolognese"], Titles(response));
    }

    [Fact]
    public async Task Search_ShouldRejectAnUnknownSort_RatherThanIgnoringIt()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var response = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&sort=whatever",
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_ShouldRejectAnUndeclaredParameter()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var response = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&sortt=title",
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("request.unknown_parameter", response.ProblemCode);
    }

    [Fact]
    public async Task Search_ShouldRequireTheHousehold_AndRefuseOneTheCallerIsNotIn()
    {
        // Arrange
        var world = await SeedAsync();

        // Act
        var missing = await world.Client.GetAsync("/api/v1/recipes", Token);
        var foreign = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={Guid.CreateVersion7()}",
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static List<string> Titles(ApiResponse response) =>
        [.. response.Json!.Value.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("title").GetString()!)];

    private async Task<(ApiClient Client, Guid HouseholdId)> SeedAsync()
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

        await SaveAsync(client, householdId, "Bolognese", null, null,
            [("beef", null), ("tomatoes", null), ("basil", null)], ["slow"]);
        await SaveAsync(client, householdId, "Lasagne", 20, 40,
            [("ricotta", null), ("basil", null), ("pasta", null)], ["vegetarian"]);
        await SaveAsync(client, householdId, "Omelette", 5, 5,
            [("eggs", null), ("butter", null)], ["quick", "vegetarian"]);

        return (client, householdId);
    }

    private static async Task SaveAsync(
        ApiClient client,
        Guid householdId,
        string title,
        int? prep,
        int? cook,
        (string Name, string? Unit)[] ingredients,
        string[] tags)
    {
        var created = await client.PostAsync(
            "/api/v1/recipes",
            new { householdId, title },
            Token);
        var recipeId = created.Json!.Value.GetProperty("recipeId").GetGuid();
        var read = await client.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{recipeId}")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new
            {
                title,
                language = "en",
                yieldAmount = 4,
                yieldKind = "servings",
                prepMinutes = prep,
                cookMinutes = cook,
                groups = new[]
                {
                    new
                    {
                        name = (string?)null,
                        ingredients = ingredients
                            .Select(line => new { name = line.Name, unit = line.Unit })
                            .ToArray()
                    }
                },
                steps = new[]
                {
                    new { segments = new[] { new { type = "text", value = "Cook it." } } }
                },
                tags
            })
        };

        request.Headers.IfMatch.Add(System.Net.Http.Headers.EntityTagHeaderValue.Parse(read.ETag!));

        await client.SendAsync(request, Token);
    }
}
