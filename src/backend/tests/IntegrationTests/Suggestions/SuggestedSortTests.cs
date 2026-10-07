using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Suggestions;

/// <summary>
/// <c>GET /recipes?sort=suggested</c>: the library ranked by the suggestion score. It composes with every filter,
/// and pages on a score, a worse cursor key than a timestamp.
/// </summary>
[Collection(RequiresDatabase.Name)]
public class SuggestedSortTests(PostgresFixture postgres)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SuggestedSort_ShouldPageWithoutRepeatingOrSkipping_OneAtATime()
    {
        // Arrange
        // The score is computed against the day, not the instant; against a clock, scores drift
        // between requests and a recipe can appear on two pages or none.
        var world = await SuggestionWorld.NewAsync(postgres);

        List<Guid> written = [];

        for (var index = 0; index < 12; index++)
        {
            var recipeId = await world.WriteAsync($"Recipe {index:00}", ingredients: [$"thing {index}"]);
            written.Add(recipeId);

            if (index % 3 == 0)
            {
                await world.CookedAsync(recipeId, daysAgo: 10 + index);
            }
        }

        List<Guid> seen = [];
        string? cursor = null;

        // Act
        do
        {
            var page = await world.Client.GetAsync(
                $"/api/v1/recipes?householdId={world.HouseholdId}&sort=suggested&limit=1"
                + (cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}"),
                Token);

            seen.AddRange(page.Json!.Value.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("recipeId").GetGuid()));

            cursor = page.Json!.Value.GetProperty("nextCursor").GetString();
        }
        while (cursor is not null);

        // Assert
        Assert.Equal(12, seen.Count);
        Assert.Equal(seen.Count, seen.Distinct().Count());
        Assert.Equal(written.Order(), seen.Order());
    }

    [Fact]
    public async Task SuggestedSort_ShouldComposeWithEveryOtherFilter()
    {
        // Arrange
        var world = await SuggestionWorld.NewAsync(postgres);

        await world.WriteAsync("Quick soup", ingredients: ["stock"], tags: ["suppe"], prep: 5, cook: 15);
        await world.WriteAsync("Slow soup", ingredients: ["stock"], tags: ["suppe"], prep: 20, cook: 180);
        await world.WriteAsync("Quick salad", ingredients: ["lettuce"], tags: ["salat"], prep: 10);

        // Act
        var response = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&sort=suggested&tag=suppe&maxMinutes=30",
            Token);

        // Assert
        var titles = response.Json!.Value.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("title").GetString())
            .ToList();

        Assert.Equal(["Quick soup"], titles);
    }

    [Fact]
    public async Task SuggestedSort_ShouldHideWhatThisPersonDismissed_ButLeaveTheRecipeAlone()
    {
        // Arrange
        // A dismissal hides a recipe from suggestions only, not from the collection or search.
        var world = await SuggestionWorld.NewAsync(postgres);

        await world.WriteAsync("Kept");
        var hidden = await world.WriteAsync("Hidden from suggestions");
        await world.DismissAsync(hidden);

        // Act
        var suggested = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&sort=suggested",
            Token);
        var plain = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}",
            Token);
        var searched = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&query=Hidden",
            Token);

        // Assert
        Assert.Equal(1, suggested.Json!.Value.GetProperty("total").GetInt32());
        Assert.Equal(2, plain.Json!.Value.GetProperty("total").GetInt32());
        Assert.Equal(1, searched.Json!.Value.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task SuggestedSort_ShouldCountWhatItReturns()
    {
        // Arrange
        // Count and page come from one statement so they cannot disagree; a scoring join that multiplies rows would break it.
        var world = await SuggestionWorld.NewAsync(postgres);

        for (var index = 0; index < 7; index++)
        {
            var recipeId = await world.WriteAsync($"Recipe {index}", tags: ["alltag"]);
            await world.CookedAsync(recipeId, daysAgo: index + 1);
            await world.PlanAsync(recipeId, DateOnly.FromDateTime(DateTime.UtcNow), "dinner");
        }

        // Act
        var response = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&sort=suggested&limit=3",
            Token);

        // Assert
        Assert.Equal(7, response.Json!.Value.GetProperty("total").GetInt32());
        Assert.Equal(3, response.Json!.Value.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task SuggestedSort_ShouldCarryTheLastCookedDate_SoACardCanSayNotSinceApril()
    {
        // Arrange
        var world = await SuggestionWorld.NewAsync(postgres);

        var cooked = await world.WriteAsync("Made once");
        await world.CookedAsync(cooked, daysAgo: 40);
        await world.WriteAsync("Never made");

        // Act
        var response = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&sort=title",
            Token);

        // Assert
        var items = response.Json!.Value.GetProperty("items").EnumerateArray().ToList();
        var made = items.Single(item => item.GetProperty("title").GetString() == "Made once");
        var never = items.Single(item => item.GetProperty("title").GetString() == "Never made");

        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, made.GetProperty("lastCookedAt").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, never.GetProperty("lastCookedAt").ValueKind);
    }

    [Fact]
    public async Task SuggestedSort_ShouldStayCorrect_OnALibraryLargerThanAnyHousehold()
    {
        // Arrange
        // Two hundred recipes with history, several times a realistic household.
        // Asserts row counts, not a wall clock (flaky in a shared container); the regression to catch
        // is a join that multiplies rows. Measured separately in September 2026: ~25 ms end to end.
        var world = await SuggestionWorld.NewAsync(postgres);

        for (var index = 0; index < 200; index++)
        {
            var recipeId = await world.WriteAsync(
                $"Recipe {index:000}",
                ingredients: [$"ingredient {index % 40}", "salt", "olive oil"],
                tags: [$"tag {index % 12}"],
                prep: 10 + (index % 30),
                cook: index % 90);

            if (index % 4 == 0)
            {
                await world.CookedAsync(recipeId, daysAgo: index % 300);
            }
        }

        // Gather statistics as autovacuum would; otherwise the planner sees a row or two and nests loops
        // over every scoring CTE (3 s instead of 40 ms, past the command timeout on CI).
        await postgres.ExecuteAsync("analyze;", Token);

        // Act
        var suggestions = await world.SuggestAsync("&limit=5");
        var listed = await world.Client.GetAsync(
            $"/api/v1/recipes?householdId={world.HouseholdId}&sort=suggested&limit=24",
            Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, suggestions.StatusCode);
        Assert.Equal(5, SuggestionWorld.Ids(suggestions).Distinct().Count());

        var page = listed.Json!.Value.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("recipeId").GetGuid())
            .ToList();

        Assert.Equal(200, listed.Json!.Value.GetProperty("total").GetInt32());
        Assert.Equal(24, page.Count);
        Assert.Equal(page.Count, page.Distinct().Count());
    }
}
