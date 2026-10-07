using Application.Abstractions;

namespace IntegrationTests.Suggestions.Replay;

/// <summary>
/// Whether the shortlists were any good to look at, not whether they predicted dinner
/// (<c>docs/suggestions-research.md</c> §M.2).
/// </summary>
/// <remarks>
/// Catches the failure a small catalogue is prone to (the same eight recipes forever) without
/// anyone judging. Computed from replayed shortlists, not <c>suggestion_impressions</c>: a write on
/// every render was not worth it.
/// </remarks>
/// <param name="Coverage">
/// Distinct recipes shown over the library size; well above 0.3 is healthy, near 0.05 is
/// over-specialised.
/// </param>
/// <param name="RepetitionRate">
/// Share of shown recipes the same person also saw within the previous week.
/// </param>
/// <param name="NoveltyShare">Share of shown recipes this person had never cooked.</param>
/// <param name="Gini">
/// Inequality of how often each recipe was shown: 0 is even, near 1 is one recipe every time.
/// </param>
internal sealed record ShortlistQuality(double Coverage, double RepetitionRate, double NoveltyShare, double Gini)
{
    /// <summary>
    /// How many of each replayed list a person sees: what the front page asks for.
    /// </summary>
    internal const int Shown = SuggestionContext.DefaultCount;

    private static readonly TimeSpan RepeatWindow = TimeSpan.FromDays(7);

    internal static ShortlistQuality Of(IReadOnlyList<ReplayOutcome> outcomes)
    {
        if (outcomes.Count == 0)
        {
            return new ShortlistQuality(0, 0, 0, 0);
        }

        var library = outcomes.Max(outcome => outcome.LibrarySize);
        var shown = outcomes.SelectMany(outcome => outcome.Suggested.Take(Shown)).ToList();
        var timesShown = shown.GroupBy(scored => scored.Recipe.RecipeId).Select(group => group.Count()).ToList();

        return new ShortlistQuality(
            (double)timesShown.Count / library,
            RepeatsWithinAWeek(outcomes),
            shown.Average(scored => scored.Recipe.CookCount == 0 ? 1.0 : 0.0),
            GiniOf([.. timesShown, .. Enumerable.Repeat(0, Math.Max(0, library - timesShown.Count))]));
    }

    private static double RepeatsWithinAWeek(IReadOnlyList<ReplayOutcome> outcomes)
    {
        var repeats = 0;
        var total = 0;

        foreach (var outcome in outcomes)
        {
            var earlier = outcomes
                .Where(other => other.Point.UserId == outcome.Point.UserId
                    && other.Point.MadeAt < outcome.Point.MadeAt
                    && other.Point.MadeAt >= outcome.Point.MadeAt - RepeatWindow)
                .SelectMany(other => other.Suggested.Take(Shown))
                .Select(scored => scored.Recipe.RecipeId)
                .ToHashSet();

            foreach (var scored in outcome.Suggested.Take(Shown))
            {
                total++;

                if (earlier.Contains(scored.Recipe.RecipeId))
                {
                    repeats++;
                }
            }
        }

        return total == 0 ? 0 : (double)repeats / total;
    }

    private static double GiniOf(List<int> counts)
    {
        var sum = counts.Sum();

        if (sum == 0)
        {
            return 0;
        }

        counts.Sort();

        var weighted = 0.0;

        for (var index = 0; index < counts.Count; index++)
        {
            weighted += (index + 1.0) * counts[index];
        }

        return (2 * weighted / (counts.Count * (double)sum)) - ((counts.Count + 1.0) / counts.Count);
    }
}
