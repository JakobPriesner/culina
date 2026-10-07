using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Suggestions;

/// <summary>
/// The promise: a request gets <c>min(asked for, eligible)</c> suggestions, where eligible is the
/// household's recipes minus what the caller excluded and what this person hid. Nothing else
/// removes a candidate.
/// </summary>
/// <remarks>
/// The file to break loudest: a recommender returning nothing fails quietly. It holds because every
/// term is a score and none a threshold: a recipe eaten this morning sinks, never leaves.
/// </remarks>
[Collection(RequiresDatabase.Name)]
public class SuggestionGuaranteeTests(PostgresFixture postgres)
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(0, 5, 0)]
    [InlineData(1, 5, 1)]
    [InlineData(3, 5, 3)]
    [InlineData(5, 5, 5)]
    [InlineData(9, 5, 5)]
    [InlineData(9, 1, 1)]
    [InlineData(9, 12, 9)]
    [InlineData(20, 12, 12)]
    public async Task Suggestions_ShouldReturnAsManyAsExist_UpToWhatWasAskedFor(
        int recipes,
        int asked,
        int expected)
    {
        var world = await SuggestionWorld.NewAsync(postgres);

        for (var index = 0; index < recipes; index++)
        {
            await world.WriteAsync($"Recipe {index:00}");
        }

        var response = await world.SuggestAsync($"&limit={asked}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, SuggestionWorld.Titles(response).Count);
    }

    [Fact]
    public async Task Suggestions_ShouldFillTheSet_WhenNobodyHasEverCookedAnything()
    {
        // The cold start: no history means no taste profile, so every similarity divides by an
        // empty vector; a ranker that turns that into a filter returns an empty screen on import
        // day.
        var world = await SuggestionWorld.NewAsync(postgres);

        for (var index = 0; index < 8; index++)
        {
            await world.WriteAsync($"Untouched {index}", tags: ["neu"]);
        }

        var response = await world.SuggestAsync("&limit=5");

        Assert.Equal(5, SuggestionWorld.Titles(response).Count);
    }

    [Fact]
    public async Task Suggestions_ShouldFillTheSet_WhenEveryRecipeWasCookedToday()
    {
        // The saturated household: every candidate carries the full repetition penalty, so a filter
        // on "score above zero" returns nothing.
        var world = await SuggestionWorld.NewAsync(postgres);

        for (var index = 0; index < 6; index++)
        {
            var recipeId = await world.WriteAsync($"Eaten {index}");
            await world.CookedAsync(recipeId, daysAgo: 0);
        }

        var response = await world.SuggestAsync("&limit=5");

        Assert.Equal(5, SuggestionWorld.Titles(response).Count);
    }

    [Fact]
    public async Task Suggestions_ShouldFillTheSet_WhenOneRecipeDominatesTheHistory()
    {
        // One recipe cooked constantly and nothing else touched: a profile collapsing onto one item
        // tends to return just that.
        var world = await SuggestionWorld.NewAsync(postgres);

        var favourite = await world.WriteAsync("Pasta", ingredients: ["pasta", "butter"]);

        for (var day = 1; day <= 30; day++)
        {
            await world.CookedAsync(favourite, daysAgo: day);
        }

        for (var index = 0; index < 6; index++)
        {
            await world.WriteAsync($"Other {index}", ingredients: [$"thing {index}"]);
        }

        var response = await world.SuggestAsync("&limit=5");

        Assert.Equal(5, SuggestionWorld.Titles(response).Count);
    }

    [Fact]
    public async Task Suggestions_ShouldStillAnswer_WhenEveryRecipeHasNothingButATitle()
    {
        // A recipe with only a title is valid, so ranking must hold with every null the scoring
        // query can meet: no tags, ingredients, times or steps.
        var world = await SuggestionWorld.NewAsync(postgres);

        for (var index = 0; index < 5; index++)
        {
            await world.Client.PostAsync(
                "/api/v1/recipes",
                new { householdId = world.HouseholdId, title = $"Just a title {index}" },
                Token);
        }

        var response = await world.SuggestAsync("&limit=5");

        Assert.Equal(5, SuggestionWorld.Titles(response).Count);
    }

    [Fact]
    public async Task Suggestions_ShouldHonourADismissal_RatherThanFillingTheSetAnyway()
    {
        // The one place the guarantee stops, on purpose: a dismissal is what the person asked for,
        // and topping up with what they hid would be the app arguing.
        var world = await SuggestionWorld.NewAsync(postgres);

        var first = await world.WriteAsync("Kept");
        var hidden = await world.WriteAsync("Hidden");

        await world.DismissAsync(hidden);

        var response = await world.SuggestAsync("&limit=5");

        Assert.Equal(["Kept"], SuggestionWorld.Titles(response));
        Assert.DoesNotContain(hidden, SuggestionWorld.Ids(response));
        Assert.Contains(first, SuggestionWorld.Ids(response));
    }

    [Fact]
    public async Task Suggestions_ShouldBringBackADismissal_WhenItIsUndone()
    {
        var world = await SuggestionWorld.NewAsync(postgres);
        var recipeId = await world.WriteAsync("Second thoughts");

        await world.DismissAsync(recipeId);

        var hidden = await world.SuggestAsync();
        await world.Client.DeleteAsync(
            $"/api/v1/recipes/{recipeId}/suggestion-dismissal",
            Token);
        var restored = await world.SuggestAsync();

        Assert.Empty(SuggestionWorld.Titles(hidden));
        Assert.Equal(["Second thoughts"], SuggestionWorld.Titles(restored));
    }

    [Fact]
    public async Task Suggestions_ShouldNeverRepeatARecipe_WithinOneAnswer()
    {
        // Ten CTEs join onto the recipe row; any that yields two rows for one recipe duplicates it,
        // and the diversity pass would pick it twice.
        var world = await SuggestionWorld.NewAsync(postgres);

        for (var index = 0; index < 6; index++)
        {
            var recipeId = await world.WriteAsync(
                $"Busy {index}",
                ingredients: ["onion", "garlic", "oil"],
                tags: ["schnell", "vegetarisch"]);

            // Several of everything (cook log, plan entries, a second member's history) fans out
            // from the same recipe id.
            await world.CookedAsync(recipeId, daysAgo: 40);
            await world.CookedAsync(recipeId, daysAgo: 80);
            await world.PlanAsync(recipeId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), "dinner");
            await world.PlanAsync(recipeId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2), "lunch");
        }

        var response = await world.SuggestAsync("&limit=6");

        var ids = SuggestionWorld.Ids(response);
        Assert.Equal(6, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task Suggestions_ShouldReturnTheSameAnswerTwice_OnTheSameDay()
    {
        // The jitter is seeded by the day, not by chance: otherwise the list reshuffles on every
        // refresh and the first answer looks arbitrary.
        var world = await SuggestionWorld.NewAsync(postgres);

        for (var index = 0; index < 8; index++)
        {
            await world.WriteAsync($"Stable {index}");
        }

        var first = await world.SuggestAsync("&limit=5");
        var second = await world.SuggestAsync("&limit=5");

        Assert.Equal(SuggestionWorld.Ids(first), SuggestionWorld.Ids(second));
    }

    [Fact]
    public async Task Suggestions_ShouldKeepItsPromise_WhenTheCallerExcludesMostOfTheKitchen()
    {
        // What the meal planner does: it knows what is on this week and wants none of it offered
        // again.
        var world = await SuggestionWorld.NewAsync(postgres);

        List<Guid> written = [];

        for (var index = 0; index < 7; index++)
        {
            written.Add(await world.WriteAsync($"Recipe {index}"));
        }

        var excluded = string.Concat(written.Take(5).Select(id => $"&exclude={id}"));

        var response = await world.SuggestAsync($"&limit=5{excluded}");

        Assert.Equal(2, SuggestionWorld.Ids(response).Count);
        Assert.DoesNotContain(written[0], SuggestionWorld.Ids(response));
    }

    [Fact]
    public async Task Suggestions_ShouldReturnNothing_RatherThanIgnoringATimeCeiling()
    {
        // The guarantee is about not losing candidates to the system's own opinions, never
        // inventing ones the caller ruled out: "I have twenty minutes" is not a preference to
        // outvote.
        var world = await SuggestionWorld.NewAsync(postgres);

        await world.WriteAsync("Braise", prep: 30, cook: 180);
        await world.WriteAsync("Also a braise", prep: 20, cook: 120);

        var response = await world.SuggestAsync("&limit=5&maxMinutes=20");

        Assert.Empty(SuggestionWorld.Titles(response));
    }
}
