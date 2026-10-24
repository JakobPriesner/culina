using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Users;

[Collection(RequiresDatabase.Name)]
public class DiaryEndpointTests(PostgresFixture postgres)
{
    private const string Day = "2026-10-22";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Diary_ShouldStayClosed_UntilSwitchedOnWithAgreementToTheCurrentWording()
    {
        // Arrange
        var kitchen = await Kitchen.OpenAsync(postgres);

        // Act
        var before = await kitchen.Client.GetAsync("/api/v1/users/me/settings/diary", Token);
        var refused = await kitchen.Client.PostAsync("/api/v1/users/me/diary/entries", Typed("Kaffee", 5), Token);
        var unagreed = await kitchen.Client.PutAsync("/api/v1/users/me/settings/diary", new { enabled = true }, Token);
        var agreed = await kitchen.Client.PutAsync(
            "/api/v1/users/me/settings/diary",
            new { enabled = true, consentVersion = 1 },
            Token);

        // Assert
        Assert.False(before.Json!.Value.GetProperty("enabled").GetBoolean());
        Assert.Equal(1, before.Json!.Value.GetProperty("currentConsentVersion").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("diary.disabled", refused.ProblemCode);
        Assert.Equal(HttpStatusCode.BadRequest, unagreed.StatusCode);
        Assert.Equal("diary.consent_required", unagreed.ProblemCode);
        Assert.Equal(HttpStatusCode.OK, agreed.StatusCode);
        Assert.True(agreed.Json!.Value.GetProperty("enabled").GetBoolean());
        Assert.Equal(1, agreed.Json!.Value.GetProperty("consentVersion").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, agreed.Json!.Value.GetProperty("consentedAt").ValueKind);
    }

    [Fact]
    public async Task AFood_ShouldBeLookedUpInTheTable_AndAddUpIntoTheDay()
    {
        // Arrange
        var kitchen = await OpenDiaryAsync();
        var oats = await FoodAsync(kitchen, "Haferflocken");

        // Act
        var added = await kitchen.Client.PostAsync(
            "/api/v1/users/me/diary/entries",
            new { date = Day, meal = "breakfast", foodCode = oats.Code, amount = 50m },
            Token);
        var day = await DayAsync(kitchen.Client);

        // Assert
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var entry = added.Json!.Value.GetProperty("entry");
        Assert.Equal("food", entry.GetProperty("source").GetString());
        Assert.Equal("g", entry.GetProperty("unit").GetString());
        Assert.False(entry.GetProperty("estimated").GetBoolean());
        var kcal = entry.GetProperty("figures").GetProperty("kcal").GetDecimal();
        Assert.Equal(Math.Round(oats.KcalPer100Grams / 2, 3), kcal);

        Assert.Equal(kcal, day.GetProperty("totals").GetProperty("kcal").GetDecimal());
        Assert.False(day.GetProperty("atLeast").GetBoolean());
        Assert.Equal(0m, day.GetProperty("typedKcal").GetDecimal());
    }

    [Fact]
    public async Task AFoodInATypicalUnit_ShouldBeAnEstimate_ThatNamesItsSource()
    {
        // Arrange
        var kitchen = await OpenDiaryAsync();

        // Act
        // G480100 is Zwiebel, with a cited typical weight per piece.
        var added = await kitchen.Client.PostAsync(
            "/api/v1/users/me/diary/entries",
            new { date = Day, meal = "dinner", foodCode = "G480100", amount = 2m, unit = "piece" },
            Token);
        var unknownUnit = await kitchen.Client.PostAsync(
            "/api/v1/users/me/diary/entries",
            new { date = Day, meal = "dinner", foodCode = "G480100", amount = 1m, unit = "bunch" },
            Token);

        // Assert
        var entry = added.Json!.Value.GetProperty("entry");
        Assert.True(entry.GetProperty("estimated").GetBoolean());
        Assert.Equal(220m, entry.GetProperty("grams").GetDecimal());
        Assert.Contains("FDC", entry.GetProperty("basis").GetString(), StringComparison.Ordinal);
        Assert.Equal("diary.unit_unknown", unknownUnit.ProblemCode);
    }

    [Fact]
    public async Task ARecipePortion_ShouldKeepItsFigures_WhenTheRecipeChangesLater()
    {
        // Arrange
        var kitchen = await OpenDiaryAsync();
        var recipeId = await RecipeAsync(kitchen, flourGrams: 400m);
        var perPortion = (await kitchen.Client.GetAsync($"/api/v1/recipes/{recipeId}/nutrition", Token))
            .Json!.Value.GetProperty("values").GetProperty("energyKcal").GetProperty("value").GetDecimal();

        // Act
        var added = await kitchen.Client.PostAsync(
            "/api/v1/users/me/diary/entries",
            new { date = Day, meal = "lunch", recipeId, portions = 1.5m },
            Token);
        await SaveRecipeAsync(kitchen.Client, recipeId, flourGrams: 800m);
        var day = await DayAsync(kitchen.Client);

        // Assert
        var entry = added.Json!.Value.GetProperty("entry");
        Assert.Equal("recipe", entry.GetProperty("source").GetString());
        Assert.Equal("Brot", entry.GetProperty("label").GetString());
        Assert.Equal(Math.Round(perPortion * 1.5m, 3), entry.GetProperty("figures").GetProperty("kcal").GetDecimal());
        var kept = Assert.Single(day.GetProperty("entries").EnumerateArray().ToList());
        Assert.Equal(
            entry.GetProperty("figures").GetProperty("kcal").GetDecimal(),
            kept.GetProperty("figures").GetProperty("kcal").GetDecimal());
    }

    [Fact]
    public async Task ATypedEntry_ShouldKeepUnknownMacrosUnknown_AndCountAsTyped()
    {
        // Arrange
        var kitchen = await OpenDiaryAsync();

        // Act
        var added = await kitchen.Client.PostAsync("/api/v1/users/me/diary/entries", Typed("Café-Lunch", 650), Token);
        var implausible = await kitchen.Client.PostAsync(
            "/api/v1/users/me/diary/entries",
            Typed("Ein Berg", 50000),
            Token);
        var day = await DayAsync(kitchen.Client);

        // Assert
        var figures = added.Json!.Value.GetProperty("entry").GetProperty("figures");
        Assert.Equal(650m, figures.GetProperty("kcal").GetDecimal());
        Assert.Equal(JsonValueKind.Null, figures.GetProperty("protein").ValueKind);
        Assert.Equal("diary.implausible", implausible.ProblemCode);
        Assert.Equal(650m, day.GetProperty("typedKcal").GetDecimal());
    }

    [Fact]
    public async Task AnEntry_ShouldMoveWithItsFigures_AndBeLookedUpAgain_WhenItsAmountChanges()
    {
        // Arrange
        var kitchen = await OpenDiaryAsync();
        var oats = await FoodAsync(kitchen, "Haferflocken");
        var entryId = (await kitchen.Client.PostAsync(
                "/api/v1/users/me/diary/entries",
                new { date = Day, meal = "breakfast", foodCode = oats.Code, amount = 50m },
                Token))
            .Json!.Value.GetProperty("entry").GetProperty("entryId").GetGuid();

        // Act
        var moved = await kitchen.Client.PatchAsync(
            $"/api/v1/users/me/diary/entries/{entryId}",
            new { meal = "snack" },
            Token);
        var doubled = await kitchen.Client.PatchAsync(
            $"/api/v1/users/me/diary/entries/{entryId}",
            new { amount = 100m },
            Token);

        // Assert
        var before = moved.Json!.Value.GetProperty("entry");
        var after = doubled.Json!.Value.GetProperty("entry");
        Assert.Equal("snack", before.GetProperty("meal").GetString());
        Assert.Equal("snack", after.GetProperty("meal").GetString());
        Assert.Equal(
            before.GetProperty("figures").GetProperty("kcal").GetDecimal() * 2,
            after.GetProperty("figures").GetProperty("kcal").GetDecimal());
        Assert.Equal(entryId, after.GetProperty("entryId").GetGuid());
    }

    [Fact]
    public async Task OwnFigures_ShouldBeCorrectable_ButALookedUpEntrysShouldNot()
    {
        // Arrange
        var kitchen = await OpenDiaryAsync();
        var oats = await FoodAsync(kitchen, "Haferflocken");
        var typedId = (await kitchen.Client.PostAsync(
                "/api/v1/users/me/diary/entries",
                new { date = Day, meal = "lunch", label = "Café-Lunch", kcal = 6500m, protein = 30m },
                Token))
            .Json!.Value.GetProperty("entry").GetProperty("entryId").GetGuid();
        var foodId = (await kitchen.Client.PostAsync(
                "/api/v1/users/me/diary/entries",
                new { date = Day, meal = "breakfast", foodCode = oats.Code, amount = 50m },
                Token))
            .Json!.Value.GetProperty("entry").GetProperty("entryId").GetGuid();

        // Act
        var corrected = await kitchen.Client.PatchAsync(
            $"/api/v1/users/me/diary/entries/{typedId}",
            new { label = "Café-Lunch mit Suppe", kcal = 650m, fat = 20m },
            Token);
        var implausible = await kitchen.Client.PatchAsync(
            $"/api/v1/users/me/diary/entries/{typedId}",
            new { kcal = 50000m },
            Token);
        var lookedUp = await kitchen.Client.PatchAsync(
            $"/api/v1/users/me/diary/entries/{foodId}",
            new { kcal = 10m },
            Token);
        var day = await DayAsync(kitchen.Client);

        // Assert
        var entry = corrected.Json!.Value.GetProperty("entry");
        Assert.Equal(typedId, entry.GetProperty("entryId").GetGuid());
        Assert.Equal("Café-Lunch mit Suppe", entry.GetProperty("label").GetString());
        Assert.Equal("lunch", entry.GetProperty("meal").GetString());
        Assert.Equal(650m, entry.GetProperty("figures").GetProperty("kcal").GetDecimal());
        Assert.Equal(20m, entry.GetProperty("figures").GetProperty("fat").GetDecimal());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("figures").GetProperty("protein").ValueKind);
        Assert.Equal("diary.implausible", implausible.ProblemCode);
        Assert.Equal("diary.unclear_source", lookedUp.ProblemCode);
        Assert.Equal(650m, day.GetProperty("typedKcal").GetDecimal());
    }

    [Fact]
    public async Task ADiary_ShouldBeInvisible_ToEveryoneElse()
    {
        // Arrange
        var kitchen = await OpenDiaryAsync();
        var entryId = (await kitchen.Client.PostAsync("/api/v1/users/me/diary/entries", Typed("Kuchen", 400), Token))
            .Json!.Value.GetProperty("entry").GetProperty("entryId").GetGuid();
        using var stranger = await Kitchen.StrangerAsync(postgres);
        await stranger.PutAsync("/api/v1/users/me/settings/diary", new { enabled = true, consentVersion = 1 }, Token);

        // Act
        var theirDay = await DayAsync(stranger);
        var change = await stranger.PatchAsync($"/api/v1/users/me/diary/entries/{entryId}", new { meal = "lunch" }, Token);
        var removal = await stranger.DeleteAsync($"/api/v1/users/me/diary/entries/{entryId}", Token);

        // Assert
        Assert.Empty(theirDay.GetProperty("entries").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, change.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, removal.StatusCode);
        Assert.Single((await DayAsync(kitchen.Client)).GetProperty("entries").EnumerateArray());
    }

    [Fact]
    public async Task SwitchingOff_ShouldKeepTheEntries_OrDeleteThem_AsThePersonChooses()
    {
        // Arrange
        var kitchen = await OpenDiaryAsync();
        await kitchen.Client.PostAsync("/api/v1/users/me/diary/entries", Typed("Kuchen", 400), Token);

        // Act
        await kitchen.Client.PutAsync("/api/v1/users/me/settings/diary", new { enabled = false }, Token);
        var whileOff = await kitchen.Client.GetAsync($"/api/v1/users/me/diary?from={Day}", Token);
        await kitchen.Client.PutAsync("/api/v1/users/me/settings/diary", new { enabled = true, consentVersion = 1 }, Token);
        var kept = await DayAsync(kitchen.Client);
        await kitchen.Client.PutAsync(
            "/api/v1/users/me/settings/diary",
            new { enabled = false, eraseEntries = true },
            Token);
        await kitchen.Client.PutAsync("/api/v1/users/me/settings/diary", new { enabled = true, consentVersion = 1 }, Token);
        var erased = await DayAsync(kitchen.Client);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, whileOff.StatusCode);
        Assert.Single(kept.GetProperty("entries").EnumerateArray());
        Assert.Empty(erased.GetProperty("entries").EnumerateArray());
    }

    [Fact]
    public async Task ARange_ShouldHaveEveryDay_AndRefuseOneThatIsTooLong()
    {
        // Arrange
        var kitchen = await OpenDiaryAsync();

        // Act
        var week = await kitchen.Client.GetAsync("/api/v1/users/me/diary?from=2026-10-19&to=2026-10-25", Token);
        var tooLong = await kitchen.Client.GetAsync("/api/v1/users/me/diary?from=2026-01-01&to=2026-12-31", Token);

        // Assert
        Assert.Equal(7, week.Json!.Value.GetProperty("days").GetArrayLength());
        Assert.Equal("diary.invalid_dates", tooLong.ProblemCode);
    }

    private async Task<Kitchen> OpenDiaryAsync()
    {
        var kitchen = await Kitchen.OpenAsync(postgres);
        var on = await kitchen.Client.PutAsync(
            "/api/v1/users/me/settings/diary",
            new { enabled = true, consentVersion = 1 },
            Token);

        Assert.Equal(HttpStatusCode.OK, on.StatusCode);

        return kitchen;
    }

    private static object Typed(string label, decimal kcal) =>
        new { date = Day, meal = "lunch", label, kcal };

    private static async Task<JsonElement> DayAsync(ApiClient client)
    {
        var read = await client.GetAsync($"/api/v1/users/me/diary?from={Day}", Token);

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        return read.Json!.Value.GetProperty("days")[0];
    }

    private static async Task<(string Code, decimal KcalPer100Grams)> FoodAsync(Kitchen kitchen, string name)
    {
        var found = (await kitchen.Client.GetAsync($"/api/v1/foods?q={name}", Token)).Json!.Value
            .GetProperty("items")[0];

        return (found.GetProperty("code").GetString()!, found.GetProperty("energyKcal").GetDecimal());
    }

    private static async Task<Guid> RecipeAsync(Kitchen kitchen, decimal flourGrams)
    {
        var recipeId = (await kitchen.Client.PostAsync(
            "/api/v1/recipes",
            new { householdId = kitchen.HouseholdId, title = "Brot" },
            Token)).Json!.Value.GetProperty("recipeId").GetGuid();

        await SaveRecipeAsync(kitchen.Client, recipeId, flourGrams);

        return recipeId;
    }

    private static async Task SaveRecipeAsync(ApiClient client, Guid recipeId, decimal flourGrams)
    {
        var read = await client.GetAsync($"/api/v1/recipes/{recipeId}", Token);
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/recipes/{recipeId}")
        {
            Content = JsonContent.Create(new
            {
                title = "Brot",
                language = "de",
                yieldAmount = 4,
                yieldKind = "servings",
                groups = new[]
                {
                    new
                    {
                        name = (string?)null,
                        ingredients = new[] { new { name = "Mehl", quantity = (decimal?)flourGrams, unit = "g" } }
                    }
                },
                steps = Array.Empty<object>(),
                tags = Array.Empty<string>()
            })
        };
        request.Headers.IfMatch.Add(EntityTagHeaderValue.Parse(read.ETag!));

        var response = await client.SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
