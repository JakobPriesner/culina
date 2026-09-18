using System.Net;
using IntegrationTests.Fixtures;

namespace IntegrationTests.Suggestions;

/// <summary>
/// The promise: a request gets <c>min(asked for, eligible)</c> suggestions,
/// where eligible is the household's recipes minus what the caller excluded and
/// minus what this person has hidden. Nothing else removes a candidate.
/// </summary>
/// <remarks>
/// <para>
/// This is the file to read first, and the one to break loudest. A recommender
/// that returns nothing is the normal failure of the genre, and it fails
/// quietly: the screen looks like an empty kitchen rather than like a bug, so
/// nobody reports it. Every case below is a way that has actually happened to
/// somebody's implementation.
/// </para>
/// <para>
/// The mechanism that makes them pass is a design rule rather than a fallback
/// ladder: <b>every term is a score and no term is a threshold.</b> A recipe the
/// household ate this morning sinks to the bottom of the list; it never leaves
/// it. There is nothing to "fall back" to because nothing was taken away.
/// </para>
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
        // Arrange
        var world = await SuggestionWorld.NewAsync(postgres);

        for (var index = 0; index < recipes; index++)
        {
            await world.WriteAsync($"Recipe {index:00}");
        }

        // Act
        var response = await world.SuggestAsync($"&limit={asked}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expected, SuggestionWorld.Titles(response).Count);
    }

    [Fact]
    public async Task Suggestions_ShouldFillTheSet_WhenNobodyHasEverCookedAnything()
    {
        // Arrange
        // The cold start, and the case a content-based ranker gets wrong: with
        // no history there is no taste profile, so every similarity is a
        // division by an empty vector. A ranker that let that become a filter
        // returns an empty screen on the day somebody imports their library.
        var world = await SuggestionWorld.NewAsync(postgres);

        for (var index = 0; index < 8; index++)
        {
            await world.WriteAsync($"Untouched {index}", tags: ["neu"]);
        }

        // Act
        var response = await world.SuggestAsync("&limit=5");

        // Assert
        Assert.Equal(5, SuggestionWorld.Titles(response).Count);
    }

    [Fact]
    public async Task Suggestions_ShouldFillTheSet_WhenEveryRecipeWasCookedToday()
    {
        // Arrange
        // The saturated household: every candidate carries the full repetition
        // penalty, so every score is deeply negative. Anything that filtered on
        // "score above zero" — a tempting line to write — returns nothing here.
        var world = await SuggestionWorld.NewAsync(postgres);

        for (var index = 0; index < 6; index++)
        {
            var recipeId = await world.WriteAsync($"Eaten {index}");
            await world.CookedAsync(recipeId, daysAgo: 0);
        }

        // Act
        var response = await world.SuggestAsync("&limit=5");

        // Assert
        Assert.Equal(5, SuggestionWorld.Titles(response).Count);
    }

    [Fact]
    public async Task Suggestions_ShouldFillTheSet_WhenOneRecipeDominatesTheHistory()
    {
        // Arrange
        // The opposite shape: one recipe cooked constantly and nothing else
        // touched. A ranker whose profile collapses onto a single item tends to
        // return that item and little else.
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

        // Act
        var response = await world.SuggestAsync("&limit=5");

        // Assert
        Assert.Equal(5, SuggestionWorld.Titles(response).Count);
    }

    [Fact]
    public async Task Suggestions_ShouldStillAnswer_WhenEveryRecipeHasNothingButATitle()
    {
        // Arrange
        // "A recipe with only a title is valid" is a promise the domain makes,
        // so the ranking has to hold with no tags, no ingredients, no times and
        // no steps — which is every null the scoring query can meet at once.
        var world = await SuggestionWorld.NewAsync(postgres);

        for (var index = 0; index < 5; index++)
        {
            await world.Client.PostAsync(
                "/api/v1/recipes",
                new { householdId = world.HouseholdId, title = $"Just a title {index}" },
                Token);
        }

        // Act
        var response = await world.SuggestAsync("&limit=5");

        // Assert
        Assert.Equal(5, SuggestionWorld.Titles(response).Count);
    }

    [Fact]
    public async Task Suggestions_ShouldHonourADismissal_RatherThanFillingTheSetAnyway()
    {
        // Arrange
        // The one place the guarantee stops, and it stops on purpose: a
        // dismissal is what this person explicitly asked for, and topping the
        // list back up with the thing they just hid would be the app arguing
        // with them.
        var world = await SuggestionWorld.NewAsync(postgres);

        var first = await world.WriteAsync("Kept");
        var hidden = await world.WriteAsync("Hidden");

        await world.DismissAsync(hidden);

        // Act
        var response = await world.SuggestAsync("&limit=5");

        // Assert
        Assert.Equal(["Kept"], SuggestionWorld.Titles(response));
        Assert.DoesNotContain(hidden, SuggestionWorld.Ids(response));
        Assert.Contains(first, SuggestionWorld.Ids(response));
    }

    [Fact]
    public async Task Suggestions_ShouldBringBackADismissal_WhenItIsUndone()
    {
        // Arrange
        var world = await SuggestionWorld.NewAsync(postgres);
        var recipeId = await world.WriteAsync("Second thoughts");

        await world.DismissAsync(recipeId);

        // Act
        var hidden = await world.SuggestAsync();
        await world.Client.DeleteAsync(
            $"/api/v1/recipes/{recipeId}/suggestion-dismissal",
            Token);
        var restored = await world.SuggestAsync();

        // Assert
        Assert.Empty(SuggestionWorld.Titles(hidden));
        Assert.Equal(["Second thoughts"], SuggestionWorld.Titles(restored));
    }

    [Fact]
    public async Task Suggestions_ShouldNeverRepeatARecipe_WithinOneAnswer()
    {
        // Arrange
        // Ten CTEs join onto the recipe row, and any one of them that can
        // produce two rows for one recipe silently duplicates it. The diversity
        // pass would then happily pick the same recipe twice.
        var world = await SuggestionWorld.NewAsync(postgres);

        for (var index = 0; index < 6; index++)
        {
            var recipeId = await world.WriteAsync(
                $"Busy {index}",
                ingredients: ["onion", "garlic", "oil"],
                tags: ["schnell", "vegetarisch"]);

            // Several of everything: cook log entries, plan entries and a
            // second member's history all fan out from the same recipe id.
            await world.CookedAsync(recipeId, daysAgo: 40);
            await world.CookedAsync(recipeId, daysAgo: 80);
            await world.PlanAsync(recipeId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), "dinner");
            await world.PlanAsync(recipeId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2), "lunch");
        }

        // Act
        var response = await world.SuggestAsync("&limit=6");

        // Assert
        var ids = SuggestionWorld.Ids(response);
        Assert.Equal(6, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task Suggestions_ShouldReturnTheSameAnswerTwice_OnTheSameDay()
    {
        // Arrange
        // The exploration jitter is seeded by the day, not by chance. Without
        // that the list reshuffles under a thumb on every refresh, which reads
        // as the app fidgeting and makes the first answer look arbitrary.
        var world = await SuggestionWorld.NewAsync(postgres);

        for (var index = 0; index < 8; index++)
        {
            await world.WriteAsync($"Stable {index}");
        }

        // Act
        var first = await world.SuggestAsync("&limit=5");
        var second = await world.SuggestAsync("&limit=5");

        // Assert
        Assert.Equal(SuggestionWorld.Ids(first), SuggestionWorld.Ids(second));
    }

    [Fact]
    public async Task Suggestions_ShouldKeepItsPromise_WhenTheCallerExcludesMostOfTheKitchen()
    {
        // Arrange
        // What the meal planner does: it already knows what is on this week and
        // does not want any of it offered again.
        var world = await SuggestionWorld.NewAsync(postgres);

        List<Guid> written = [];

        for (var index = 0; index < 7; index++)
        {
            written.Add(await world.WriteAsync($"Recipe {index}"));
        }

        var excluded = string.Concat(written.Take(5).Select(id => $"&exclude={id}"));

        // Act
        var response = await world.SuggestAsync($"&limit=5{excluded}");

        // Assert
        Assert.Equal(2, SuggestionWorld.Ids(response).Count);
        Assert.DoesNotContain(written[0], SuggestionWorld.Ids(response));
    }

    [Fact]
    public async Task Suggestions_ShouldReturnNothing_RatherThanIgnoringATimeCeiling()
    {
        // Arrange
        // The guarantee is about not losing candidates to the system's own
        // opinions, never about inventing ones the caller ruled out. "I have
        // twenty minutes" is not a preference to be outvoted.
        var world = await SuggestionWorld.NewAsync(postgres);

        await world.WriteAsync("Braise", prep: 30, cook: 180);
        await world.WriteAsync("Also a braise", prep: 20, cook: 120);

        // Act
        var response = await world.SuggestAsync("&limit=5&maxMinutes=20");

        // Assert
        Assert.Empty(SuggestionWorld.Titles(response));
    }
}
