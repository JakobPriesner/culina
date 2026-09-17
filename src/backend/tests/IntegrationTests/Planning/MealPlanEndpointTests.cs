using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Planning;

/// <summary>
/// Moving a planned meal, end to end.
/// </summary>
/// <remarks>
/// Rearranging is the commonest edit a plan gets — a week is agreed on Sunday
/// and then argued with all week — and the part worth a real database is the
/// renumbering, which is the only place a position can quietly stop being an
/// index.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class MealPlanEndpointTests(PostgresFixture postgres)
{
    private const string Password = "correct horse battery staple";

    /// <summary>A Monday, so the week under test is the one being asked for.</summary>
    private static readonly DateOnly Monday = new(2026, 9, 14);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Move_ShouldPutTheMealOnAnotherDayWithoutRePlanningIt()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeAsync(client, householdId, "Curry");

        var planned = await client.PostAsync(
            $"/api/v1/households/{householdId}/meal-plan",
            new { date = Monday, recipeId, servings = 6, slot = "lunch" },
            Token);

        var entryId = MealsOn(planned, Monday)[0].GetProperty("entryId").GetGuid();

        // Act
        var response = await client.PatchAsync(
            $"/api/v1/households/{householdId}/meal-plan/{entryId}",
            new { date = Monday.AddDays(3) },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(MealsOn(response, Monday));

        var moved = Assert.Single(MealsOn(response, Monday.AddDays(3)));

        // What is cooked and for how many does not move with the date. Doing
        // this as a remove plus an add is exactly how both would be lost.
        Assert.Equal(6m, moved.GetProperty("servings").GetDecimal());
        Assert.Equal("lunch", moved.GetProperty("slot").GetString());
    }

    [Fact]
    public async Task Move_ShouldPlaceAMealWhereItWasDropped()
    {
        // Arrange
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

        // Act
        // The last one, dropped onto the gap above the first.
        var response = await client.PatchAsync(
            $"/api/v1/households/{householdId}/meal-plan/{third.GetProperty("entryId").GetGuid()}",
            new { date = Monday, position = 0 },
            Token);

        // Assert
        var titles = MealsOn(response, Monday)
            .Select(meal => meal.GetProperty("title").GetString())
            .ToList();

        Assert.Equal(["Third", "First", "Second"], titles);
    }

    [Fact]
    public async Task Move_ShouldCountTheGapsTheSameWayGoingDownAsGoingUp()
    {
        // Arrange
        // Moving something down a day is where a reorder goes one off: the
        // meal vacates a place above its destination on the way past. Dropping
        // into the gap below everything has to mean the bottom either way.
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

        // Act
        // The day now reads Third, First, Second. First is dragged to the gap
        // below all three of them.
        var response = await client.PatchAsync(
            $"/api/v1/households/{householdId}/meal-plan/{day[0].GetProperty("entryId").GetGuid()}",
            new { date = Monday, position = 3 },
            Token);

        // Assert
        var titles = MealsOn(response, Monday)
            .Select(meal => meal.GetProperty("title").GetString())
            .ToList();

        Assert.Equal(["Third", "Second", "First"], titles);
    }

    [Fact]
    public async Task Move_ShouldRefuseAPositionThatIsNotAnIndex()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeAsync(client, householdId, "Curry");

        var planned = await client.PostAsync(
            $"/api/v1/households/{householdId}/meal-plan",
            new { date = Monday, recipeId },
            Token);

        var entryId = MealsOn(planned, Monday)[0].GetProperty("entryId").GetGuid();

        // Act
        var response = await client.PatchAsync(
            $"/api/v1/households/{householdId}/meal-plan/{entryId}",
            new { date = Monday, position = -1 },
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Move_ShouldNotFindSomebodyElsesPlannedMeal()
    {
        // Arrange
        using var client = await SignedInAsync();
        var householdId = await HouseholdAsync(client);
        var recipeId = await RecipeAsync(client, householdId, "Curry");

        var planned = await client.PostAsync(
            $"/api/v1/households/{householdId}/meal-plan",
            new { date = Monday, recipeId },
            Token);

        var entryId = MealsOn(planned, Monday)[0].GetProperty("entryId").GetGuid();

        using var stranger = await SecondAccountAsync();

        // Act
        var response = await stranger.PatchAsync(
            $"/api/v1/households/{householdId}/meal-plan/{entryId}",
            new { date = Monday.AddDays(1) },
            Token);

        // Assert
        // 404, not 403: a stranger learns nothing about which plans exist.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
