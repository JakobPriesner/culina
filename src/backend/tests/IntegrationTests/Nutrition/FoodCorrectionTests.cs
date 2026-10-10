using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Nutrition;

[Collection(RequiresDatabase.Name)]
public class FoodCorrectionTests(PostgresFixture postgres)
{
    private const string Butter = "Q611000";

    private const string Buttermilk = "M150000";

    private const string Oats = "C131000";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Put_ShouldChangeTheFigureAndItsETag_ForEveryRecipeOfTheHouseholdUsingTheName()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var first = await RecipeAsync(kitchen, "Brot", [("Butter", 100m)]);
        var second = await RecipeAsync(kitchen, "Kuchen", [("butter", 200m), ("Mehl", 100m)]);
        var beforeFirst = await kitchen.Client.GetAsync(NutritionPath(first), Token);
        var beforeSecond = await kitchen.Client.GetAsync(NutritionPath(second), Token);

        // Act
        var put = await PutAsync(kitchen.Client, kitchen.HouseholdId, "Butter", Buttermilk);
        var afterFirst = await kitchen.Client.GetAsync(NutritionPath(first), Token);
        var afterSecond = await kitchen.Client.GetAsync(NutritionPath(second), Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        Assert.NotEqual(beforeFirst.ETag, afterFirst.ETag);
        Assert.NotEqual(beforeSecond.ETag, afterSecond.ETag);

        var line = Lines(afterFirst)[0];
        Assert.Equal(Buttermilk, line.GetProperty("food").GetProperty("code").GetString());
        Assert.True(line.GetProperty("corrected").GetBoolean());
        Assert.Equal(100m * 0.39m / 4m, line.GetProperty("energyKcal").GetDecimal());

        var butter = Lines(afterSecond)[0];
        Assert.Equal(Buttermilk, butter.GetProperty("food").GetProperty("code").GetString());
        Assert.True(butter.GetProperty("corrected").GetBoolean());
        Assert.False(Lines(afterSecond)[1].GetProperty("corrected").GetBoolean());
    }

    [Fact]
    public async Task Put_ShouldAnswerNotModified_WhenNothingChangedSinceTheCorrection()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipe = await RecipeAsync(kitchen, "Brot", [("Butter", 100m)]);
        await PutAsync(kitchen.Client, kitchen.HouseholdId, "Butter", Buttermilk);
        var read = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);

        // Act
        var again = await GetWithTagAsync(kitchen.Client, NutritionPath(recipe), read.ETag!);

        // Assert
        Assert.Equal(HttpStatusCode.NotModified, again.StatusCode);
    }

    [Fact]
    public async Task Put_ShouldLeaveAnotherHouseholdAlone()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipe = await RecipeAsync(kitchen, "Brot", [("Butter", 100m)]);
        var flat = await HeirAsync(kitchen);
        var before = await kitchen.Client.GetAsync($"{NutritionPath(recipe)}?householdId={flat}", Token);

        // Act: the heir corrects for itself.
        var put = await PutAsync(kitchen.Client, flat, "Butter", Buttermilk);
        var heir = await kitchen.Client.GetAsync($"{NutritionPath(recipe)}?householdId={flat}", Token);
        var owner = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);

        // Assert: the owner of the recipe is untouched, the heir sees its own choice.
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        Assert.NotEqual(before.ETag, heir.ETag);
        Assert.True(Lines(heir)[0].GetProperty("corrected").GetBoolean());
        Assert.False(Lines(owner)[0].GetProperty("corrected").GetBoolean());
        Assert.Equal(Butter, Lines(owner)[0].GetProperty("food").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Put_ShouldNotReachTheHouseholdAnHeirInheritsFrom()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipe = await RecipeAsync(kitchen, "Brot", [("Butter", 100m)]);
        var flat = await HeirAsync(kitchen);
        var ownerBefore = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);

        // Act
        await PutAsync(kitchen.Client, flat, "Butter", null);
        var ownerAfter = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);

        // Assert
        Assert.Equal(ownerBefore.ETag, ownerAfter.ETag);
        Assert.Equal("counted", Lines(ownerAfter)[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Put_ShouldExcludeTheLine_WhenTheFoodIsNull()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipe = await RecipeAsync(kitchen, "Brot", [("Butter", 100m), ("Mehl", 200m)]);

        // Act
        var put = await PutAsync(kitchen.Client, kitchen.HouseholdId, "Butter", null);
        var after = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        Assert.Equal("excluded", Lines(after)[0].GetProperty("status").GetString());
        Assert.True(Lines(after)[0].GetProperty("corrected").GetBoolean());
        Assert.Equal(1, after.Json!.Value.GetProperty("counted").GetInt32());
    }

    [Fact]
    public async Task Delete_ShouldRestoreTheDefault_AndBeIdempotent()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipe = await RecipeAsync(kitchen, "Brot", [("Butter", 100m)]);
        var original = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);
        await PutAsync(kitchen.Client, kitchen.HouseholdId, "Butter", Buttermilk);

        // Act
        var first = await kitchen.Client.DeleteAsync(IngredientPath(kitchen.HouseholdId, "Butter"), Token);
        var second = await kitchen.Client.DeleteAsync(IngredientPath(kitchen.HouseholdId, "Butter"), Token);
        var never = await kitchen.Client.DeleteAsync(IngredientPath(kitchen.HouseholdId, "Zimt"), Token);
        var restored = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, never.StatusCode);
        Assert.Equal(original.ETag, restored.ETag);
        Assert.False(Lines(restored)[0].GetProperty("corrected").GetBoolean());
    }

    [Fact]
    public async Task Put_ShouldShareOneCorrection_BetweenSpellingsThatFoldAlike()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipe = await RecipeAsync(kitchen, "Frühstück", [("Müsli", 50m), ("Muesli", 50m)]);

        // Act
        await PutAsync(kitchen.Client, kitchen.HouseholdId, "Muesli", Oats);
        var after = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);
        await kitchen.Client.DeleteAsync(IngredientPath(kitchen.HouseholdId, "Müsli"), Token);
        var gone = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);

        // Assert
        Assert.All(Lines(after), line => Assert.Equal(Oats, line.GetProperty("food").GetProperty("code").GetString()));
        Assert.All(Lines(after), line => Assert.True(line.GetProperty("corrected").GetBoolean()));
        Assert.All(Lines(gone), line => Assert.False(line.GetProperty("corrected").GetBoolean()));
    }

    [Theory]
    [InlineData("Salz/Pfeffer")]
    [InlineData("100% Saft & Mark")]
    [InlineData("Käse (mild)")]
    public async Task Put_ShouldRoundTripANameWithSpecialCharacters(string name)
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipe = await RecipeAsync(kitchen, "Seltsam", [(name, 100m)]);

        // Act
        var put = await PutAsync(kitchen.Client, kitchen.HouseholdId, name, Oats);
        var after = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);
        var delete = await kitchen.Client.DeleteAsync(IngredientPath(kitchen.HouseholdId, name), Token);
        var gone = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        Assert.Equal(Oats, Lines(after)[0].GetProperty("food").GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.False(Lines(gone)[0].GetProperty("corrected").GetBoolean());
    }

    [Fact]
    public async Task Put_ShouldRefuseAFoodThatIsNotInTheTable()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var response = await PutAsync(kitchen.Client, kitchen.HouseholdId, "Butter", "NOPE123");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("nutrition.unknown_food", response.ProblemCode);
    }

    [Fact]
    public async Task Put_ShouldRefuseANameThatIsTooLongOrBlank()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var tooLong = await PutAsync(kitchen.Client, kitchen.HouseholdId, new string('x', 121), Oats);
        var blank = await PutAsync(kitchen.Client, kitchen.HouseholdId, " ", Oats);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
    }

    [Fact]
    public async Task PutAndDelete_ShouldSayNotFound_ToANonMember()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var stranger = await Kitchen.StrangerAsync(postgres);

        // Act
        var put = await PutAsync(stranger, kitchen.HouseholdId, "Butter", Buttermilk);
        var delete = await stranger.DeleteAsync(IngredientPath(kitchen.HouseholdId, "Butter"), Token);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal("households.not_found", put.ProblemCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task Put_ShouldNeedASession()
    {
        // Arrange
        using var anonymous = postgres.Api.NewApiClient();

        // Act
        var response = await PutAsync(anonymous, Guid.NewGuid(), "Butter", Buttermilk);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetFoods_ShouldFindFoodsByTheirGermanAndEnglishNames_InAnyCase()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var capital = await kitchen.Client.GetAsync("/api/v1/foods?q=Butter", Token);
        var lower = await kitchen.Client.GetAsync("/api/v1/foods?q=butter", Token);
        var english = await kitchen.Client.GetAsync("/api/v1/foods?q=buttermilk", Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, capital.StatusCode);
        Assert.NotNull(capital.ETag);
        Assert.Equal(Codes(capital), Codes(lower));
        Assert.Contains(Butter, Codes(capital));
        Assert.True(Codes(capital).Count <= 20);

        var butter = capital.Json!.Value.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("code").GetString() == Butter);
        Assert.Equal("Butter mild gesäuert", butter.GetProperty("nameDe").GetString());
        Assert.Equal("Best quality butter", butter.GetProperty("nameEn").GetString());
        Assert.Equal(747m, butter.GetProperty("energyKcal").GetDecimal());

        // "Buttermilk" is only an English name; the German one is "Buttermilch".
        Assert.Contains(Buttermilk, Codes(english));
    }

    [Fact]
    public async Task GetFoods_ShouldHonourLimit_AndAnswerNotModified()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var three = await kitchen.Client.GetAsync("/api/v1/foods?q=butter&limit=3", Token);
        var again = await GetWithTagAsync(kitchen.Client, "/api/v1/foods?q=butter&limit=3", three.ETag!);
        var blank = await kitchen.Client.GetAsync("/api/v1/foods", Token);

        // Assert
        Assert.Equal(3, Codes(three).Count);
        Assert.Equal(HttpStatusCode.NotModified, again.StatusCode);
        Assert.Empty(Codes(blank));
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=51")]
    [InlineData("limit=many")]
    [InlineData("limit=")]
    public async Task GetFoods_ShouldRejectAnInvalidLimit(string query)
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var response = await kitchen.Client.GetAsync($"/api/v1/foods?q=butter&{query}", Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("request.invalid_parameter", response.ProblemCode);
    }

    [Fact]
    public async Task GetFoods_ShouldRejectAnUnknownParameter_AndNeedASession()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        using var anonymous = postgres.Api.NewApiClient();

        // Act
        var unknown = await kitchen.Client.GetAsync("/api/v1/foods?q=butter&sort=name", Token);
        var signedOut = await anonymous.GetAsync("/api/v1/foods?q=butter", Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, signedOut.StatusCode);
    }

    private static string NutritionPath(Guid recipeId) => $"/api/v1/recipes/{recipeId}/nutrition";

    private static string IngredientPath(Guid householdId, string name) =>
        $"/api/v1/households/{householdId}/ingredients/{Uri.EscapeDataString(name)}";

    private static List<string> Codes(ApiResponse response) =>
        [.. response.Json!.Value.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("code").GetString()!)];

    private static List<JsonElement> Lines(ApiResponse response) =>
        [.. response.Json!.Value.GetProperty("ingredients").EnumerateArray()];

    private static Task<ApiResponse> PutAsync(ApiClient client, Guid householdId, string name, string? food) =>
        client.PutAsync(IngredientPath(householdId, name), new { food }, Token);

    private static async Task<ApiResponse> GetWithTagAsync(ApiClient client, string path, string etag)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(etag));

        return await client.SendAsync(request, Token);
    }

    private static async Task<Guid> HeirAsync(Kitchen kitchen) =>
        (await kitchen.Client.PostAsync(
            "/api/v1/households",
            new { name = "Flat", inheritsFrom = kitchen.HouseholdId },
            Token)).Json!.Value.GetProperty("householdId").GetGuid();

    private static async Task<Guid> RecipeAsync(Kitchen kitchen, string title, (string Name, decimal Grams)[] lines)
    {
        var recipeId = (await kitchen.Client.PostAsync(
            "/api/v1/recipes",
            new { householdId = kitchen.HouseholdId, title },
            Token)).Json!.Value.GetProperty("recipeId").GetGuid();
        var read = await kitchen.Client.GetAsync($"/api/v1/recipes/{recipeId}", Token);

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{recipeId}")
        {
            Content = JsonContent.Create(new
            {
                title,
                language = "de",
                yieldAmount = 4,
                yieldKind = "servings",
                groups = new[]
                {
                    new
                    {
                        name = (string?)null,
                        ingredients = lines.Select(line => new { name = line.Name, quantity = (decimal?)line.Grams, unit = "g" }).ToArray()
                    }
                },
                steps = Array.Empty<object>(),
                tags = Array.Empty<string>()
            })
        };
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(read.ETag!));

        var response = await kitchen.Client.SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return recipeId;
    }
}
