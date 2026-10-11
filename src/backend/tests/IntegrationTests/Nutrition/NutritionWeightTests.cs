using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Nutrition;

[Collection(RequiresDatabase.Name)]
public class NutritionWeightTests(PostgresFixture postgres)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Put_ShouldChangeTheFigureAndETag_ForEveryRecipeOfTheHouseholdUsingTheNameInThatUnit()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var first = await RecipeAsync(kitchen, "Suppe", ("Zwiebel", 2m, "Stück"));
        var second = await RecipeAsync(kitchen, "Salat", ("ZWIEBEL", 1m, null), ("Zwiebel", 1m, "Bund"));
        var beforeFirst = await kitchen.Client.GetAsync(NutritionPath(first), Token);
        var beforeSecond = await kitchen.Client.GetAsync(NutritionPath(second), Token);

        // Act
        var put = await PutWeightAsync(kitchen.Client, kitchen.HouseholdId, "Zwiebel", "piece", 150m);
        var afterFirst = await kitchen.Client.GetAsync(NutritionPath(first), Token);
        var afterSecond = await kitchen.Client.GetAsync(NutritionPath(second), Token);

        // Assert: "Stück", a bare count and "piece" are one unit; a bunch is another and stays uncounted.
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        Assert.NotEqual(beforeFirst.ETag, afterFirst.ETag);
        Assert.NotEqual(beforeSecond.ETag, afterSecond.ETag);

        var typical = Lines(beforeFirst)[0];
        Assert.Equal("typicalWeight", typical.GetProperty("via").GetString());
        Assert.Equal(220m, typical.GetProperty("grams").GetDecimal());
        Assert.StartsWith("FDC", typical.GetProperty("source").GetString(), StringComparison.Ordinal);
        Assert.True(beforeFirst.Json!.Value.GetProperty("values").GetProperty("energyKcal").GetProperty("estimated").GetBoolean());

        var own = Lines(afterFirst)[0];
        Assert.Equal("householdWeight", own.GetProperty("via").GetString());
        Assert.Equal(300m, own.GetProperty("grams").GetDecimal());
        Assert.Equal("piece", own.GetProperty("unitKey").GetString());
        Assert.False(own.TryGetProperty("source", out var source) && source.ValueKind != JsonValueKind.Null);
        Assert.False(afterFirst.Json!.Value.GetProperty("values").GetProperty("energyKcal").GetProperty("estimated").GetBoolean());

        Assert.Equal(150m, Lines(afterSecond)[0].GetProperty("grams").GetDecimal());
        Assert.Equal("amountNotInGrams", Lines(afterSecond)[1].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Delete_ShouldRestoreTheTypicalWeightAndTheETag_AndBeIdempotent()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipe = await RecipeAsync(kitchen, "Suppe", ("Zwiebel", 2m, "Stück"));
        var original = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);
        await PutWeightAsync(kitchen.Client, kitchen.HouseholdId, "Zwiebel", "Stk", 150m);

        // Act
        var first = await kitchen.Client.DeleteAsync(WeightPath(kitchen.HouseholdId, "Zwiebel", "piece"), Token);
        var second = await kitchen.Client.DeleteAsync(WeightPath(kitchen.HouseholdId, "Zwiebel", "piece"), Token);
        var never = await kitchen.Client.DeleteAsync(WeightPath(kitchen.HouseholdId, "Zimt", "Stück"), Token);
        var restored = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, never.StatusCode);
        Assert.Equal(original.ETag, restored.ETag);
        Assert.Equal("typicalWeight", Lines(restored)[0].GetProperty("via").GetString());
    }

    [Fact]
    public async Task Put_ShouldLeaveAnotherHouseholdAndTheOneItInheritsFromAlone()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipe = await RecipeAsync(kitchen, "Suppe", ("Zwiebel", 2m, "Stück"));
        var flat = await HeirAsync(kitchen);
        var ownerBefore = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);

        // Act: the heir sets a weight for itself.
        var put = await PutWeightAsync(kitchen.Client, flat, "Zwiebel", "piece", 150m);
        var heir = await kitchen.Client.GetAsync($"{NutritionPath(recipe)}?householdId={flat}", Token);
        var owner = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        Assert.Equal("householdWeight", Lines(heir)[0].GetProperty("via").GetString());
        Assert.Equal(300m, Lines(heir)[0].GetProperty("grams").GetDecimal());
        Assert.Equal(ownerBefore.ETag, owner.ETag);
        Assert.Equal("typicalWeight", Lines(owner)[0].GetProperty("via").GetString());
    }

    [Fact]
    public async Task Put_ShouldRoundTripANameWithASlashInIt()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        await RecipeAsync(kitchen, "Seltsam", ("Salz/Pfeffer", 2m, "Dose"));

        // Act
        var put = await PutWeightAsync(kitchen.Client, kitchen.HouseholdId, "Salz/Pfeffer", "Dose", 80m);
        var facts = await kitchen.Client.GetAsync(FactsPath(kitchen.HouseholdId), Token);
        var delete = await kitchen.Client.DeleteAsync(WeightPath(kitchen.HouseholdId, "Salz/Pfeffer", "Dose"), Token);
        var gone = await kitchen.Client.GetAsync(FactsPath(kitchen.HouseholdId), Token);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        var weight = facts.Json!.Value.GetProperty("items")[0];
        Assert.Equal("salz/pfeffer", weight.GetProperty("name").GetString());
        Assert.Equal("can", weight.GetProperty("weights")[0].GetProperty("unit").GetString());
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(0, gone.Json!.Value.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Put_ShouldCountAMeasureNowhere_AndRefuseWhatIsNotAWeight()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var zero = await PutWeightAsync(kitchen.Client, kitchen.HouseholdId, "Zwiebel", "piece", 0m);
        var tooMuch = await PutWeightAsync(kitchen.Client, kitchen.HouseholdId, "Zwiebel", "piece", 20000m);
        var mass = await PutWeightAsync(kitchen.Client, kitchen.HouseholdId, "Zwiebel", "g", 100m);
        var volume = await PutWeightAsync(kitchen.Client, kitchen.HouseholdId, "Zwiebel", "Milliliter", 100m);
        var cup = await PutWeightAsync(kitchen.Client, kitchen.HouseholdId, "Zwiebel", "cup", 100m);
        var notAWord = await PutWeightAsync(kitchen.Client, kitchen.HouseholdId, "Zwiebel", "200g", 100m);
        var tooLong = await PutWeightAsync(kitchen.Client, kitchen.HouseholdId, new string('x', 121), "piece", 100m);
        var blank = await PutWeightAsync(kitchen.Client, kitchen.HouseholdId, " ", "piece", 100m);

        // Assert
        Assert.Equal("nutrition.invalid_grams", zero.ProblemCode);
        Assert.Equal("nutrition.invalid_grams", tooMuch.ProblemCode);
        Assert.Equal("nutrition.unit_has_a_size", mass.ProblemCode);
        Assert.Equal("nutrition.unit_has_a_size", volume.ProblemCode);
        Assert.Equal("nutrition.unit_has_a_size", cup.ProblemCode);
        Assert.Equal(HttpStatusCode.BadRequest, notAWord.StatusCode);
        Assert.Equal("shopping.name_too_long", tooLong.ProblemCode);
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.All(new[] { zero, tooMuch, mass, volume, cup, notAWord, tooLong, blank }, one => Assert.Equal(HttpStatusCode.BadRequest, one.StatusCode));
    }

    [Fact]
    public async Task PutAndDelete_ShouldSayNotFound_ToANonMember_AndNeedASession()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var stranger = await Kitchen.StrangerAsync(postgres);
        using var anonymous = postgres.Api.NewApiClient();

        // Act
        var put = await PutWeightAsync(stranger, kitchen.HouseholdId, "Zwiebel", "piece", 150m);
        var delete = await stranger.DeleteAsync(WeightPath(kitchen.HouseholdId, "Zwiebel", "piece"), Token);
        var facts = await stranger.GetAsync(FactsPath(kitchen.HouseholdId), Token);
        var settings = await stranger.GetAsync(SettingsPath(kitchen.HouseholdId), Token);
        var setSettings = await stranger.PutAsync(SettingsPath(kitchen.HouseholdId), new { useTypicalWeights = false }, Token);
        var signedOut = await PutWeightAsync(anonymous, kitchen.HouseholdId, "Zwiebel", "piece", 150m);

        // Assert
        Assert.All(new[] { put, delete, facts, settings, setSettings }, one => Assert.Equal(HttpStatusCode.NotFound, one.StatusCode));
        Assert.Equal("households.not_found", put.ProblemCode);
        Assert.Equal(HttpStatusCode.Unauthorized, signedOut.StatusCode);
    }

    [Fact]
    public async Task Settings_ShouldDefaultToTypicalWeights_AndTurningThemOffChangesTheFigureAndETag()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipe = await RecipeAsync(kitchen, "Suppe", ("Zwiebel", 2m, "Stück"), ("Mehl", 100m, "g"));
        var before = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);

        // Act
        var initial = await kitchen.Client.GetAsync(SettingsPath(kitchen.HouseholdId), Token);
        var off = await kitchen.Client.PutAsync(SettingsPath(kitchen.HouseholdId), new { useTypicalWeights = false }, Token);
        var after = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);
        var read = await kitchen.Client.GetAsync(SettingsPath(kitchen.HouseholdId), Token);
        await kitchen.Client.PutAsync(SettingsPath(kitchen.HouseholdId), new { useTypicalWeights = true }, Token);
        var on = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);

        // Assert
        Assert.True(initial.Json!.Value.GetProperty("useTypicalWeights").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.False(off.Json!.Value.GetProperty("useTypicalWeights").GetBoolean());
        Assert.False(read.Json!.Value.GetProperty("useTypicalWeights").GetBoolean());
        Assert.NotEqual(before.ETag, after.ETag);
        Assert.Equal("typicalWeight", Lines(before)[0].GetProperty("via").GetString());
        Assert.Equal("amountNotInGrams", Lines(after)[0].GetProperty("status").GetString());
        Assert.Equal("count", Lines(after)[0].GetProperty("reason").GetString());
        Assert.False(after.Json!.Value.GetProperty("values").GetProperty("energyKcal").GetProperty("estimated").GetBoolean());
        Assert.Equal(before.ETag, on.ETag);
    }

    [Fact]
    public async Task Settings_ShouldStillLetAHouseholdWeightCount_WhenTypicalWeightsAreOff()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipe = await RecipeAsync(kitchen, "Suppe", ("Zwiebel", 2m, "Stück"));
        await kitchen.Client.PutAsync(SettingsPath(kitchen.HouseholdId), new { useTypicalWeights = false }, Token);

        // Act
        await PutWeightAsync(kitchen.Client, kitchen.HouseholdId, "Zwiebel", "piece", 150m);
        var after = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);

        // Assert
        Assert.Equal("householdWeight", Lines(after)[0].GetProperty("via").GetString());
    }

    [Fact]
    public async Task Settings_ShouldBeThePlainBodyOfAnotherHouseholdsChoice_NotShared()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var flat = await HeirAsync(kitchen);

        // Act
        await kitchen.Client.PutAsync(SettingsPath(flat), new { useTypicalWeights = false }, Token);

        // Assert
        Assert.False((await kitchen.Client.GetAsync(SettingsPath(flat), Token)).Json!.Value.GetProperty("useTypicalWeights").GetBoolean());
        Assert.True((await kitchen.Client.GetAsync(SettingsPath(kitchen.HouseholdId), Token)).Json!.Value.GetProperty("useTypicalWeights").GetBoolean());
    }

    [Fact]
    public async Task GetIngredientFacts_ShouldListFoodChoicesAndWeightsByName()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        await PutWeightAsync(kitchen.Client, kitchen.HouseholdId, "Zwiebel", "Stück", 150m);
        await PutWeightAsync(kitchen.Client, kitchen.HouseholdId, "Zwiebel", "EL", 12m);
        await kitchen.Client.PutAsync(IngredientPath(kitchen.HouseholdId, "Milch"), new { food = "M111200" }, Token);
        await kitchen.Client.PutAsync(IngredientPath(kitchen.HouseholdId, "Müsli"), new { food = (string?)null }, Token);

        // Act
        var facts = await kitchen.Client.GetAsync(FactsPath(kitchen.HouseholdId), Token);

        // Assert
        var items = facts.Json!.Value.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["milch", "muesli", "zwiebel"], items.Select(item => item.GetProperty("name").GetString()).ToArray());

        var milk = items[0];
        Assert.True(milk.GetProperty("corrected").GetBoolean());
        Assert.Equal("M111200", milk.GetProperty("food").GetProperty("code").GetString());
        Assert.Equal("fettarme Milch 1,5 %", milk.GetProperty("food").GetProperty("labelDe").GetString());
        Assert.Equal(0, milk.GetProperty("weights").GetArrayLength());

        var muesli = items[1];
        Assert.True(muesli.GetProperty("corrected").GetBoolean());
        Assert.False(muesli.TryGetProperty("food", out var none) && none.ValueKind != JsonValueKind.Null);

        var onion = items[2];
        Assert.False(onion.GetProperty("corrected").GetBoolean());
        Assert.Equal(
            [("piece", 150m), ("tbsp", 12m)],
            onion.GetProperty("weights").EnumerateArray().Select(w => (w.GetProperty("unit").GetString()!, w.GetProperty("grams").GetDecimal())).ToArray());
    }

    [Fact]
    public async Task GetIngredientFacts_ShouldBeEmpty_ForAHouseholdThatSaidNothing_AndLeaveTheSuggestionsAlone()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var facts = await kitchen.Client.GetAsync(FactsPath(kitchen.HouseholdId), Token);
        var suggestions = await kitchen.Client.GetAsync($"/api/v1/households/{kitchen.HouseholdId}/ingredients?q=zwi", Token);

        // Assert
        Assert.Equal(0, facts.Json!.Value.GetProperty("items").GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, suggestions.StatusCode);
    }

    [Fact]
    public async Task Get_ShouldCountACanOfTomatoesAsCannedTomatoes_AndOfferTheFoodsVariants()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipe = await RecipeAsync(kitchen, "Sugo", ("Tomaten", 1m, "Dose"), ("Milch", 100m, "ml"), ("Mehl", 100m, "g"));

        // Act
        var read = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);

        // Assert
        var tomatoes = Lines(read)[0];
        Assert.Equal("G568900", tomatoes.GetProperty("food").GetProperty("code").GetString());
        Assert.Equal("Tomaten aus der Dose", tomatoes.GetProperty("food").GetProperty("labelDe").GetString());
        Assert.Equal(400m, tomatoes.GetProperty("grams").GetDecimal());
        Assert.Equal("can", tomatoes.GetProperty("unitKey").GetString());

        var variants = Lines(read)[1].GetProperty("variants").EnumerateArray().ToList();
        Assert.Equal("M111300", variants[0].GetProperty("code").GetString());
        var half = variants.Single(one => one.GetProperty("code").GetString() == "M111200");
        Assert.Equal("fettarme Milch 1,5 %", half.GetProperty("labelDe").GetString());
        Assert.Equal("low-fat milk 1.5 %", half.GetProperty("labelEn").GetString());
        Assert.True(half.GetProperty("energyKcal").GetDecimal() > 0m);
    }

    [Fact]
    public async Task Get_ShouldOfferTheVariants_AlsoOfACorrectedFood_AndAnUncountedOne()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipe = await RecipeAsync(kitchen, "Brei", ("Milch", 1m, "Becher"));
        await kitchen.Client.PutAsync(IngredientPath(kitchen.HouseholdId, "Milch"), new { food = "M111100" }, Token);

        // Act
        var read = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);

        // Assert
        var line = Lines(read)[0];
        Assert.Equal("amountNotInGrams", line.GetProperty("status").GetString());
        Assert.True(line.GetProperty("corrected").GetBoolean());
        Assert.Contains(line.GetProperty("variants").EnumerateArray(), one => one.GetProperty("code").GetString() == "M111300");
    }

    [Fact]
    public async Task GetShared_ShouldCountByTypicalWeights_ButNeverByAHouseholdsWeights_AndCarryVariants()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);
        var recipe = await RecipeAsync(kitchen, "Suppe", ("Zwiebel", 2m, "Stück"), ("Milch", 100m, "ml"));
        await PutWeightAsync(kitchen.Client, kitchen.HouseholdId, "Zwiebel", "piece", 150m);
        await kitchen.Client.PutAsync(SettingsPath(kitchen.HouseholdId), new { useTypicalWeights = false }, Token);
        var token = (await kitchen.Client.PutAsync($"/api/v1/recipes/{recipe}/share", new { }, Token)).Json!.Value.GetProperty("token").GetString();
        using var visitor = postgres.Api.NewApiClient();

        // Act
        var shared = await visitor.GetAsync($"/api/v1/shared-recipes/{token}/nutrition", Token);
        var own = await kitchen.Client.GetAsync(NutritionPath(recipe), Token);

        // Assert
        var onion = Lines(shared)[0];
        Assert.Equal("typicalWeight", onion.GetProperty("via").GetString());
        Assert.Equal(220m, onion.GetProperty("grams").GetDecimal());
        Assert.True(shared.Json!.Value.GetProperty("values").GetProperty("energyKcal").GetProperty("estimated").GetBoolean());
        Assert.Equal("householdWeight", Lines(own)[0].GetProperty("via").GetString());
        Assert.Contains(Lines(shared)[1].GetProperty("variants").EnumerateArray(), one => one.GetProperty("code").GetString() == "M111200");
    }

    private static string NutritionPath(Guid recipeId) => $"/api/v1/recipes/{recipeId}/nutrition";

    private static string IngredientPath(Guid householdId, string name) =>
        $"/api/v1/households/{householdId}/ingredients/{Uri.EscapeDataString(name)}";

    private static string WeightPath(Guid householdId, string name, string unit) =>
        $"{IngredientPath(householdId, name)}/units/{Uri.EscapeDataString(unit)}";

    private static string FactsPath(Guid householdId) => $"/api/v1/households/{householdId}/nutrition/ingredients";

    private static string SettingsPath(Guid householdId) => $"/api/v1/households/{householdId}/nutrition";

    private static List<JsonElement> Lines(ApiResponse response) =>
        [.. response.Json!.Value.GetProperty("ingredients").EnumerateArray()];

    private static Task<ApiResponse> PutWeightAsync(ApiClient client, Guid householdId, string name, string unit, decimal grams) =>
        client.PutAsync(WeightPath(householdId, name, unit), new { grams }, Token);

    private static async Task<Guid> HeirAsync(Kitchen kitchen) =>
        (await kitchen.Client.PostAsync(
            "/api/v1/households",
            new { name = "Flat", inheritsFrom = kitchen.HouseholdId },
            Token)).Json!.Value.GetProperty("householdId").GetGuid();

    private static async Task<Guid> RecipeAsync(Kitchen kitchen, string title, params (string Name, decimal Amount, string? Unit)[] lines)
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
                yieldAmount = 1,
                yieldKind = "servings",
                groups = new[]
                {
                    new
                    {
                        name = (string?)null,
                        ingredients = lines.Select(line => new { name = line.Name, quantity = (decimal?)line.Amount, unit = line.Unit }).ToArray()
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
