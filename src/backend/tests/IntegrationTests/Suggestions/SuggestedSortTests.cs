using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Suggestions;

/// <summary>
/// The other door onto the same scoring: the library, ranked.
/// </summary>
/// <remarks>
/// <c>GET /recipes?sort=suggested</c> is where most of the value is, because it
/// costs no new screen and composes with every filter the collection already
/// has — so "what should I cook?" and "I have twenty-five minutes and some
/// chicken" are one feature rather than two that can disagree.
/// <para>
/// It also carries the one risk the bounded endpoint does not: it pages, and a
/// score is a much worse cursor key than a timestamp. These tests exist mostly
/// to hold that.
/// </para>
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class SuggestedSortTests(PostgresFixture postgres)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SuggestedSort_ShouldPageWithoutRepeatingOrSkipping_OneAtATime()
    {
        // Arrange
        // The reason the score is computed against the DAY and not the instant.
        // Every decayed term is a function of it, so the second page resumes the
        // same order the first was cut from; against a clock, the scores drift
        // between requests and a recipe can appear on two pages or on none.
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
        // The whole argument for a sort rather than a second collection. A
        // separate suggestions screen would have to re-implement the search, the
        // tag filter and the time ceiling, and the two would drift.
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
        // A dismissal hides a recipe from suggestions. It must not remove it
        // from the collection: the recipe is still the household's, still
        // searchable, still on its shelves. "Stop suggesting this" and "delete
        // this" are not the same sentence.
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
        // The count and the page come out of one statement precisely so they
        // cannot disagree — a "showing 20 of 19" is the kind of small wrongness
        // people notice. Adding a scoring join is exactly the change that could
        // have broken it, by multiplying rows.
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
        // Two hundred recipes with history, which is several times a realistic
        // household, against ninety lines of common table expressions with
        // nothing materialised behind them.
        //
        // The assertions are about row counts rather than about a clock, and
        // deliberately. A wall-clock bound in a suite that shares one container
        // with four hundred other tests is a coin flip, not a guard — and the
        // regression it would be a proxy for is a join that multiplies rows,
        // which shows up here as a wrong count and a repeated recipe. Those are
        // exact.
        //
        // The measurement itself, taken separately with the container quiet in
        // September 2026: ~25 ms end to end, through HTTP and the session and
        // CSRF middleware. That number is the evidence behind not caching or
        // precomputing anything; re-take it before believing it still holds, by
        // running this class on its own and timing the request.
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

        // A scoring CTE that produced two rows for one recipe would inflate the
        // count, repeat a card, and quietly break every page after the first.
        Assert.Equal(200, listed.Json!.Value.GetProperty("total").GetInt32());
        Assert.Equal(24, page.Count);
        Assert.Equal(page.Count, page.Distinct().Count());
    }
}
