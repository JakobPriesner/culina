using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Infrastructure.Persistence;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Nutrition;

[Collection(RequiresDatabase.Name)]
public class NutritionEndpointTests(PostgresFixture postgres)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_ShouldReturnPerPortionNutritionWithAnETag_ForARecipeWithKnownLines()
    {
        // Arrange
        var (kitchen, recipeId, lines) = await SeededAsync();

        // Act
        var response = await kitchen.Client.GetAsync($"/api/v1/recipes/{recipeId}/nutrition", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.ETag);

        var body = response.Json!.Value;
        Assert.Equal("serving", body.GetProperty("per").GetString());
        Assert.Equal(4m, body.GetProperty("yield").GetDecimal());
        Assert.False(body.GetProperty("complete").GetBoolean());
        Assert.Equal(3, body.GetProperty("counted").GetInt32());
        Assert.Equal(5, body.GetProperty("lines").GetInt32());

        // Flour 200 g (C214100: 348 kcal and 71.77 g carbohydrate per 100 g), oil 2 tbsp = 30 ml x 0.913 g/ml
        // (Q120000: 899 kcal), 2 eggs = 102 g at size M (E111100: 135 kcal), all over 4 portions.
        var kcal = body.GetProperty("values").GetProperty("energyKcal");
        Assert.Equal((200m * 3.48m + 27.39m * 8.99m + 102m * 1.35m) / 4m, kcal.GetProperty("value").GetDecimal());
        Assert.True(kcal.GetProperty("atLeast").GetBoolean());
        Assert.Equal(
            (200m * 71.77m / 100m + 0m + 102m * 0.34m / 100m) / 4m,
            body.GetProperty("values").GetProperty("carbohydrate").GetProperty("value").GetDecimal());

        var ingredients = body.GetProperty("ingredients").EnumerateArray().ToList();
        Assert.Equal(lines, ingredients.Select(line => line.GetProperty("ingredientId").GetGuid()));
        Assert.Equal(
            ["counted", "counted", "counted", "amountNotInGrams", "noAmount"],
            ingredients.Select(line => line.GetProperty("status").GetString()));

        var flour = ingredients[0];
        Assert.Equal("C214100", flour.GetProperty("food").GetProperty("code").GetString());
        Assert.Equal(200m, flour.GetProperty("grams").GetDecimal());
        Assert.Equal("mass", flour.GetProperty("via").GetString());
        Assert.False(flour.GetProperty("corrected").GetBoolean());
        Assert.Equal(200m * 3.48m / 4m, flour.GetProperty("energyKcal").GetDecimal());
        Assert.Equal("density", ingredients[1].GetProperty("via").GetString());
        Assert.Equal("eggSize", ingredients[2].GetProperty("via").GetString());
        Assert.False(ingredients[3].TryGetProperty("grams", out var grams) && grams.ValueKind != JsonValueKind.Null);
        Assert.Equal("count", ingredients[3].GetProperty("reason").GetString());
        Assert.True(ingredients[3].GetProperty("canRaiseEnergy").GetBoolean());
        Assert.False(flour.TryGetProperty("reason", out var reason) && reason.ValueKind != JsonValueKind.Null);
        Assert.False(flour.GetProperty("canRaiseEnergy").GetBoolean());

        // Salt has no energy, so a missing amount cannot change it.
        Assert.False(ingredients[4].GetProperty("canRaiseEnergy").GetBoolean());

        var source = body.GetProperty("source");
        Assert.Equal("Bundeslebensmittelschlüssel", source.GetProperty("name").GetString());
        Assert.Equal("4.0", source.GetProperty("version").GetString());
        Assert.Equal("Max Rubner-Institut", source.GetProperty("publisher").GetString());
        Assert.Equal("CC BY 4.0", source.GetProperty("licence").GetString());
    }

    [Fact]
    public async Task Get_ShouldAnswerNotModified_WhenTheCallerHoldsTheTag()
    {
        // Arrange
        var (kitchen, recipeId, _) = await SeededAsync();
        var first = await kitchen.Client.GetAsync($"/api/v1/recipes/{recipeId}/nutrition", Token);

        // Act
        var again = await GetWithTagAsync(kitchen.Client, $"/api/v1/recipes/{recipeId}/nutrition", first.ETag!);

        // Assert
        Assert.Equal(HttpStatusCode.NotModified, again.StatusCode);
    }

    [Fact]
    public async Task Get_ShouldChangeTheETag_WhenTheRecipeIsSaved()
    {
        // Arrange
        var (kitchen, recipeId, _) = await SeededAsync();
        var first = await kitchen.Client.GetAsync($"/api/v1/recipes/{recipeId}/nutrition", Token);
        var recipe = await kitchen.Client.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        // Act
        await SaveAsync(kitchen.Client, recipeId, recipe.ETag!, [("Mehl", 100m, "g")]);
        var changed = await GetWithTagAsync(kitchen.Client, $"/api/v1/recipes/{recipeId}/nutrition", first.ETag!);

        // Assert
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.NotEqual(first.ETag, changed.ETag);
        Assert.True(changed.Json!.Value.GetProperty("complete").GetBoolean());
    }

    [Fact]
    public async Task Get_ShouldNotWriteAnything()
    {
        // Arrange
        var (kitchen, recipeId, _) = await SeededAsync();
        var before = await kitchen.Client.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        // Act
        await kitchen.Client.GetAsync($"/api/v1/recipes/{recipeId}/nutrition", Token);
        var after = await kitchen.Client.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        // Assert: a save would have bumped the version, which is the recipe's tag.
        Assert.Equal(before.ETag, after.ETag);
    }

    [Fact]
    public async Task Get_ShouldReadAnInheritedRecipe_ForAMemberOfTheHeir()
    {
        // Arrange
        var (kitchen, recipeId, _) = await SeededAsync();
        var flat = (await kitchen.Client.PostAsync(
            "/api/v1/households",
            new { name = "Flat", inheritsFrom = kitchen.HouseholdId },
            Token)).Json!.Value.GetProperty("householdId").GetGuid();
        var grace = await Kitchen.StrangerAsync(postgres);
        await JoinAsync(flat, grace);

        // Act
        var own = await grace.GetAsync($"/api/v1/recipes/{recipeId}/nutrition", Token);
        var asHeir = await grace.GetAsync($"/api/v1/recipes/{recipeId}/nutrition?householdId={flat}", Token);
        var asOwner = await grace.GetAsync($"/api/v1/recipes/{recipeId}/nutrition?householdId={kitchen.HouseholdId}", Token);

        // Assert: without a household the recipe's own is assumed, which Grace is not in.
        Assert.Equal(HttpStatusCode.NotFound, own.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asHeir.StatusCode);
        Assert.Equal(3, asHeir.Json!.Value.GetProperty("counted").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, asOwner.StatusCode);
    }

    [Fact]
    public async Task Get_ShouldTagEachHouseholdApart_ForTheSameRecipe()
    {
        // Arrange
        var (kitchen, recipeId, _) = await SeededAsync();
        var flat = (await kitchen.Client.PostAsync(
            "/api/v1/households",
            new { name = "Flat", inheritsFrom = kitchen.HouseholdId },
            Token)).Json!.Value.GetProperty("householdId").GetGuid();

        // Act
        var own = await kitchen.Client.GetAsync($"/api/v1/recipes/{recipeId}/nutrition", Token);
        var heir = await kitchen.Client.GetAsync($"/api/v1/recipes/{recipeId}/nutrition?householdId={flat}", Token);

        // Assert
        Assert.NotEqual(own.ETag, heir.ETag);
    }

    [Fact]
    public async Task Get_ShouldSayNotFound_ForAStrangerAndForAnUnknownRecipe()
    {
        // Arrange
        var (kitchen, recipeId, _) = await SeededAsync();
        var stranger = await Kitchen.StrangerAsync(postgres);

        // Act
        var invisible = await stranger.GetAsync($"/api/v1/recipes/{recipeId}/nutrition", Token);
        var unknown = await kitchen.Client.GetAsync($"/api/v1/recipes/{Guid.NewGuid()}/nutrition", Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, invisible.StatusCode);
        Assert.Equal(unknown.ProblemCode, invisible.ProblemCode);
        Assert.Equal("recipes.not_found", invisible.ProblemCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task Get_ShouldRejectAHouseholdIdThatIsNotAnId()
    {
        // Arrange
        var (kitchen, recipeId, _) = await SeededAsync();

        // Act
        var response = await kitchen.Client.GetAsync($"/api/v1/recipes/{recipeId}/nutrition?householdId=abc", Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("request.invalid_parameter", response.ProblemCode);
    }

    [Fact]
    public async Task Get_ShouldNeedASession()
    {
        // Arrange
        var (_, recipeId, _) = await SeededAsync();
        using var anonymous = postgres.Api.NewApiClient();

        // Act
        var response = await anonymous.GetAsync($"/api/v1/recipes/{recipeId}/nutrition", Token);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<(Kitchen Kitchen, Guid RecipeId, List<Guid> Lines)> SeededAsync()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipeId = (await kitchen.Client.PostAsync(
            "/api/v1/recipes",
            new { householdId = kitchen.HouseholdId, title = "Pfannkuchen" },
            Token)).Json!.Value.GetProperty("recipeId").GetGuid();
        var read = await kitchen.Client.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        await SaveAsync(
            kitchen.Client,
            recipeId,
            read.ETag!,
            [("Mehl", 200m, "g"), ("Olivenöl", 2m, "tbsp"), ("Ei", 2m, null), ("Zwiebel", 1m, null), ("Salz", null, null)]);

        var saved = await kitchen.Client.GetAsync($"/api/v1/recipes/{recipeId}", Token);
        var lines = saved.Json!.Value.GetProperty("groups")[0].GetProperty("ingredients").EnumerateArray()
            .Select(line => line.GetProperty("ingredientId").GetGuid())
            .ToList();

        return (kitchen, recipeId, lines);
    }

    private static async Task SaveAsync(
        ApiClient client,
        Guid recipeId,
        string etag,
        (string Name, decimal? Amount, string? Unit)[] ingredients)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{recipeId}")
        {
            Content = JsonContent.Create(new
            {
                title = "Pfannkuchen",
                language = "de",
                yieldAmount = 4,
                yieldKind = "servings",
                groups = new[]
                {
                    new
                    {
                        name = (string?)null,
                        ingredients = ingredients
                            .Select(line => new { name = line.Name, quantity = line.Amount, unit = line.Unit })
                            .ToArray()
                    }
                },
                steps = Array.Empty<object>(),
                tags = Array.Empty<string>()
            })
        };
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(etag));

        var response = await client.SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<ApiResponse> GetWithTagAsync(ApiClient client, string path, string etag)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(etag));

        return await client.SendAsync(request, Token);
    }

    private async Task JoinAsync(Guid householdId, ApiClient member)
    {
        var memberId = (await member.GetAsync("/api/v1/users/me", Token)).Json!.Value.GetProperty("userId").GetGuid();

        await using var session = postgres.NewSession();

        await new DbExecutor(session).ExecuteAsync(
            """
            insert into household_members (household_id, user_id, role, joined_at)
            values (@householdId, @memberId, 'member', now());
            """,
            new { householdId, memberId },
            Token);
    }
}
