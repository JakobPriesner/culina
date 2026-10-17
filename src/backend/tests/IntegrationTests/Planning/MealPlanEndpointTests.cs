using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Planning;

/// <summary>Moving a planned meal end to end; the part worth a real database is the renumbering.</summary>
[Collection(RequiresDatabase.Name)]
public class MealPlanEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    private static readonly DateOnly Monday = new(2026, 9, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Move_ShouldPutTheMealOnAnotherDayWithoutRePlanningIt()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeAsync(client, householdId, "Curry");

        var planned = await client.PostAsync(
            $"/api/v1/households/{householdId}/meal-plan",
            new { date = Monday, recipeId, servings = 6, slot = "lunch" },
            Token);

        var entryId = MealsOn(planned, Monday)[0].GetProperty("entryId").GetGuid();

        var response = await client.PatchAsync(
            $"/api/v1/households/{householdId}/meal-plan/{entryId}",
            new { date = Monday.AddDays(3) },
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(MealsOn(response, Monday));

        var moved = Assert.Single(MealsOn(response, Monday.AddDays(3)));

        // Servings and the like must survive the move (a remove plus add would lose them).
        Assert.Equal(6m, moved.GetProperty("servings").GetDecimal());
        Assert.Equal("lunch", moved.GetProperty("slot").GetString());
    }

    [Fact]
    public async Task Move_ShouldChangeForHowManyTheMealIsWhenAsked_AndOnlyThen()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeAsync(client, householdId, "Curry");

        var planned = await client.PostAsync(
            $"/api/v1/households/{householdId}/meal-plan",
            new { date = Monday, recipeId, servings = 8 },
            Token);

        var entryId = MealsOn(planned, Monday)[0].GetProperty("entryId").GetGuid();
        var recipeServings = MealsOn(planned, Monday)[0].GetProperty("recipeServings").GetDecimal();

        var changed = await client.PatchAsync(
            $"/api/v1/households/{householdId}/meal-plan/{entryId}",
            new { date = Monday, servings = 3 },
            Token);

        var meal = Assert.Single(MealsOn(changed, Monday));

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(3m, meal.GetProperty("servings").GetDecimal());
        // The recipe is not the meal: its own servings stay as written.
        Assert.Equal(recipeServings, meal.GetProperty("recipeServings").GetDecimal());

        var moved = await client.PatchAsync(
            $"/api/v1/households/{householdId}/meal-plan/{entryId}",
            new { date = Monday.AddDays(1) },
            Token);

        Assert.Equal(3m, Assert.Single(MealsOn(moved, Monday.AddDays(1))).GetProperty("servings").GetDecimal());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public async Task Move_ShouldRefuseServingsNobodyCooks(int servings)
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeAsync(client, householdId, "Curry");

        var planned = await client.PostAsync(
            $"/api/v1/households/{householdId}/meal-plan",
            new { date = Monday, recipeId, servings = 4 },
            Token);

        var entryId = MealsOn(planned, Monday)[0].GetProperty("entryId").GetGuid();

        var response = await client.PatchAsync(
            $"/api/v1/households/{householdId}/meal-plan/{entryId}",
            new { date = Monday, servings },
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Move_ShouldPlaceAMealWhereItWasDropped()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        foreach (var title in new[] { "First", "Second", "Third" })
        {
            await client.PostAsync(
                $"/api/v1/households/{householdId}/meal-plan",
                new { date = Monday, recipeId = await RecipeAsync(client, householdId, title) },
                Token);
        }

        var third = MealsOn(
            await client.GetAsync(
                $"/api/v1/households/{householdId}/meal-plan?from={Monday:yyyy-MM-dd}",
                Token),
            Monday)[2];

        var response = await client.PatchAsync(
            $"/api/v1/households/{householdId}/meal-plan/{third.GetProperty("entryId").GetGuid()}",
            new { date = Monday, position = 0 },
            Token);

        var titles = MealsOn(response, Monday)
            .Select(meal => meal.GetProperty("title").GetString())
            .ToList();

        Assert.Equal(["Third", "First", "Second"], titles);
    }

    [Fact]
    public async Task Move_ShouldCountTheGapsTheSameWayGoingDownAsGoingUp()
    {
        // Moving down a day is where reorders go off by one; dropping below everything must mean the bottom.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        foreach (var title in new[] { "First", "Second", "Third" })
        {
            await client.PostAsync(
                $"/api/v1/households/{householdId}/meal-plan",
                new { date = Monday, recipeId = await RecipeAsync(client, householdId, title) },
                Token);
        }

        var day = MealsOn(
            await client.GetAsync(
                $"/api/v1/households/{householdId}/meal-plan?from={Monday:yyyy-MM-dd}",
                Token),
            Monday);

        await client.PatchAsync(
            $"/api/v1/households/{householdId}/meal-plan/{day[2].GetProperty("entryId").GetGuid()}",
            new { date = Monday, position = 0 },
            Token);

        var response = await client.PatchAsync(
            $"/api/v1/households/{householdId}/meal-plan/{day[0].GetProperty("entryId").GetGuid()}",
            new { date = Monday, position = 3 },
            Token);

        var titles = MealsOn(response, Monday)
            .Select(meal => meal.GetProperty("title").GetString())
            .ToList();

        Assert.Equal(["Third", "Second", "First"], titles);
    }

    [Fact]
    public async Task Move_ShouldPlaceAMealWhereItWasDroppedOnADayWithGaps()
    {
        // Removing a meal leaves its number unused, so a day is not numbered from zero.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        foreach (var title in new[] { "First", "Second", "Third", "Fourth" })
        {
            await client.PostAsync(
                $"/api/v1/households/{householdId}/meal-plan",
                new { date = Monday, recipeId = await RecipeAsync(client, householdId, title) },
                Token);
        }

        var day = MealsOn(
            await client.GetAsync(
                $"/api/v1/households/{householdId}/meal-plan?from={Monday:yyyy-MM-dd}",
                Token),
            Monday);

        await client.DeleteAsync(
            $"/api/v1/households/{householdId}/meal-plan/{day[0].GetProperty("entryId").GetGuid()}",
            Token);

        var response = await client.PatchAsync(
            $"/api/v1/households/{householdId}/meal-plan/{day[3].GetProperty("entryId").GetGuid()}",
            new { date = Monday, position = 1 },
            Token);

        var titles = MealsOn(response, Monday)
            .Select(meal => meal.GetProperty("title").GetString())
            .ToList();

        Assert.Equal(["Second", "Fourth", "Third"], titles);
    }

    [Fact]
    public async Task Move_ShouldRefuseAPositionThatIsNotAnIndex()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeAsync(client, householdId, "Curry");

        var planned = await client.PostAsync(
            $"/api/v1/households/{householdId}/meal-plan",
            new { date = Monday, recipeId },
            Token);

        var entryId = MealsOn(planned, Monday)[0].GetProperty("entryId").GetGuid();

        var response = await client.PatchAsync(
            $"/api/v1/households/{householdId}/meal-plan/{entryId}",
            new { date = Monday, position = -1 },
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Move_ShouldNotFindSomebodyElsesPlannedMeal()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeAsync(client, householdId, "Curry");

        var planned = await client.PostAsync(
            $"/api/v1/households/{householdId}/meal-plan",
            new { date = Monday, recipeId },
            Token);

        var entryId = MealsOn(planned, Monday)[0].GetProperty("entryId").GetGuid();

        using var stranger = await SecondAccountAsync();

        var response = await stranger.PatchAsync(
            $"/api/v1/households/{householdId}/meal-plan/{entryId}",
            new { date = Monday.AddDays(1) },
            Token);

        // 404, not 403: a stranger learns nothing about which plans exist.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("0001-01-03")]
    [InlineData("9999-12-28")]
    [InlineData("9999-12-31")]
    public async Task Get_ShouldRefuseADayTooNearTheEdgeOfTheCalendar_NotFail(string from)
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);

        var response = await client.GetAsync($"/api/v1/households/{householdId}/meal-plan?from={from}", Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("planning.invalid_date", response.Json!.Value.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Plan_ShouldRefuseADayAtTheEndOfTheCalendar_AndStoreNothing()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeAsync(client, householdId, "Curry");

        var response = await client.PostAsync(
            $"/api/v1/households/{householdId}/meal-plan",
            new { date = "9999-12-31", recipeId },
            Token);

        var stored = await postgres.QuerySingleAsync<long>("select count(*) from meal_plan_entries", Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, stored);
    }

    [Fact]
    public async Task Move_ShouldRefuseADayAtTheEndOfTheCalendar_AndKeepTheMealWhereItWas()
    {
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeAsync(client, householdId, "Curry");

        var planned = await client.PostAsync(
            $"/api/v1/households/{householdId}/meal-plan",
            new { date = Monday, recipeId },
            Token);
        var entryId = MealsOn(planned, Monday)[0].GetProperty("entryId").GetGuid();

        var response = await client.PatchAsync(
            $"/api/v1/households/{householdId}/meal-plan/{entryId}",
            new { date = "0001-01-01" },
            Token);

        var week = await client.GetAsync($"/api/v1/households/{householdId}/meal-plan?from={Monday:yyyy-MM-dd}", Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single(MealsOn(week, Monday));
    }

    [Fact]
    public async Task Get_ShouldSayWhenTheMealWasCooked_OnlyNearItsDay()
    {
        // The week shows a meal as done once it is cooked; a cook on another day is not this meal.
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeAsync(client, householdId, "Curry");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await client.PostAsync($"/api/v1/households/{householdId}/meal-plan", new { date = today, recipeId }, Token);
        await client.PostAsync(
            $"/api/v1/households/{householdId}/meal-plan",
            new { date = today.AddDays(10), recipeId },
            Token);
        await client.PostAsync($"/api/v1/recipes/{recipeId}/cook-log", new { householdId }, Token);

        var thisWeek = await client.GetAsync(
            $"/api/v1/households/{householdId}/meal-plan?from={today:yyyy-MM-dd}",
            Token);
        var later = await client.GetAsync(
            $"/api/v1/households/{householdId}/meal-plan?from={today.AddDays(10):yyyy-MM-dd}",
            Token);

        var cooked = Assert.Single(MealsOn(thisWeek, today)).GetProperty("cookedAt");
        Assert.Equal(System.Text.Json.JsonValueKind.String, cooked.ValueKind);
        Assert.False(
            Assert.Single(MealsOn(later, today.AddDays(10))).TryGetProperty("cookedAt", out var notYet)
            && notYet.ValueKind != System.Text.Json.JsonValueKind.Null);
    }

    private static List<System.Text.Json.JsonElement> MealsOn(ApiResponse week, DateOnly date) =>
        [
            .. week.Json!.Value.GetProperty("days")
                .EnumerateArray()
                .Single(day => day.GetProperty("date").GetString() == date.ToString("yyyy-MM-dd"))
                .GetProperty("meals")
                .EnumerateArray()
        ];

    private static async Task<Guid> HouseholdAsync(ApiClient client)
    {
        var me = await client.GetAsync("/api/v1/users/me", Token);

        return me.Json!.Value.GetProperty("households")[0].GetProperty("householdId").GetGuid();
    }

    private static async Task<Guid> RecipeAsync(ApiClient client, Guid householdId, string title)
    {
        var created = await client.PostAsync(
            "/api/v1/recipes",
            new { householdId, title },
            Token);

        return created.Json!.Value.GetProperty("recipeId").GetGuid();
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

    private async Task<ApiClient> SecondAccountAsync()
    {
        using var admin = postgres.Api.NewApiClient();

        await admin.PostAsync(
            "/api/v1/sessions",
            new { email = "ada@example.com", password = Password },
            Token);
        await admin.PutAsync(
            "/api/v1/settings/registration",
            new { openRegistration = true, requireInvitation = false, maxUsers = 100 },
            Token);

        var client = postgres.Api.NewApiClient();

        await client.PostAsync(
            "/api/v1/users",
            new { email = "grace@example.com", displayName = "Grace", password = Password },
            Token);
        await client.PostAsync(
            "/api/v1/sessions",
            new { email = "grace@example.com", password = Password },
            Token);

        return client;
    }
}
