using Application.Abstractions;

namespace IntegrationTests.Suggestions.Replay;

/// <summary>
/// The arithmetic of the four shortlist metrics, on lists written by hand.
/// </summary>
/// <remarks>
/// No database: these are sums over lists, and the replay tests already prove
/// the lists are the ones the ranker would have shown.
/// </remarks>
public class ShortlistQualityTests
{
    private static readonly Guid Ada = Guid.NewGuid();

    private static readonly DateTimeOffset Monday = new(2026, 3, 2, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Of_ShouldReportTheWorstCase_WhenTheSameRecipeLeadsEveryList()
    {
        // Arrange
        var staple = Guid.NewGuid();
        var outcomes = Enumerable.Range(0, 4)
            .Select(day => Outcome(Monday.AddDays(day), library: 10, (staple, CookCount: 3)))
            .ToList();

        // Act
        var quality = ShortlistQuality.Of(outcomes);

        // Assert
        Assert.Equal(0.1, quality.Coverage, precision: 6);
        // The first evening has nothing earlier to repeat; the next three do.
        Assert.Equal(0.75, quality.RepetitionRate, precision: 6);
        Assert.Equal(0, quality.NoveltyShare);
        Assert.Equal(0.9, quality.Gini, precision: 6);
    }

    [Fact]
    public void Of_ShouldReportTheBestCase_WhenEveryRecipeIsShownOnceToSomebodyWhoHasCookedNone()
    {
        // Arrange
        var outcomes = Enumerable.Range(0, 4)
            .Select(day => Outcome(Monday.AddDays(day), library: 4, (Guid.NewGuid(), CookCount: 0)))
            .ToList();

        // Act
        var quality = ShortlistQuality.Of(outcomes);

        // Assert
        Assert.Equal(1, quality.Coverage, precision: 6);
        Assert.Equal(0, quality.RepetitionRate);
        Assert.Equal(1, quality.NoveltyShare, precision: 6);
        Assert.Equal(0, quality.Gini, precision: 6);
    }

    [Fact]
    public void Of_ShouldNotCountARepeat_WhenTheEarlierListWasMoreThanAWeekAgo()
    {
        // Arrange
        var staple = Guid.NewGuid();
        List<ReplayOutcome> outcomes =
        [
            Outcome(Monday, library: 10, (staple, CookCount: 1)),
            Outcome(Monday.AddDays(8), library: 10, (staple, CookCount: 1))
        ];

        // Act
        var quality = ShortlistQuality.Of(outcomes);

        // Assert
        Assert.Equal(0, quality.RepetitionRate);
    }

    [Fact]
    public void Of_ShouldOnlyJudgeTheShortlist_NotTheRestOfTheReplayedList()
    {
        // Arrange
        // Ten are replayed so recall@10 can be read; five are what the front page
        // shows, and only what was shown can have been repetitive.
        var list = Enumerable.Range(0, 10).Select(_ => (Guid.NewGuid(), CookCount: 0)).ToArray();

        // Act
        var quality = ShortlistQuality.Of([Outcome(Monday, library: 20, list)]);

        // Assert
        Assert.Equal(0.25, quality.Coverage, precision: 6);
    }

    private static ReplayOutcome Outcome(
        DateTimeOffset at,
        int library,
        params (Guid RecipeId, int CookCount)[] suggested) =>
        new(
            new ReplayPoint { UserId = Ada, RecipeId = Guid.NewGuid(), MadeAt = at },
            [.. suggested.Select(one => Scored(one.RecipeId, one.CookCount))],
            library);

    private static ScoredRecipe Scored(Guid recipeId, int cookCount) =>
        new(
            new RecipeSearchRow(
                recipeId,
                "Recipe",
                ImageId: null,
                TotalMinutes: 30,
                YieldAmount: 4,
                YieldKind: "servings",
                YieldLabel: null,
                Tags: [],
                CookCount: cookCount,
                LastCookedAt: null,
                UpdatedAt: DateTimeOffset.UnixEpoch,
                MatchedIngredients: 0,
                IngredientCount: 0,
                AddedToCookbookAt: null),
            Score: 1,
            Terms: [],
            Features: []);
}
