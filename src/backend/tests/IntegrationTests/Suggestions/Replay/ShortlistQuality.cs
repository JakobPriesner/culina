using Application.Abstractions;

namespace IntegrationTests.Suggestions.Replay;

/// <summary>
/// Whether the shortlists were any good to look at, as opposed to whether they
/// predicted dinner. See <c>docs/suggestions-research.md</c> §M.2.
/// </summary>
/// <remarks>
/// <para>
/// These are the four numbers that catch the failure a small catalogue is prone
/// to — the same eight recipes forever — and none of them needs a person to say
/// anything. A ranker can score well on recall by suggesting the household's
/// three staples every night; it cannot also score well here.
/// </para>
/// <para>
/// The research document computes them from <c>suggestion_impressions</c>.
/// That table was deliberately not built: a write on every render is a real cost,
/// and the replay already knows exactly what the front page would have shown on
/// every evening it predicts. So they are computed from the replayed shortlists,
/// which also means they are available for a weight vector nobody has shipped.
/// </para>
/// </remarks>
/// <param name="Coverage">
/// Distinct recipes shown over the library size. Well above 0.3 is healthy; a
/// collapse towards 0.05 means the profile has over-specialised.
/// </param>
/// <param name="RepetitionRate">
/// Share of shown recipes the same person was also shown within the previous
/// week. Near zero is the promise the daily seed and the repetition term make.
/// </param>
/// <param name="NoveltyShare">Share of shown recipes this person had never cooked.</param>
/// <param name="Gini">
/// Inequality of how often each recipe was shown, zero for perfectly even and
/// approaching one for a single recipe every time. One number for "is it
/// showing eight recipes forever?".
/// </param>
internal sealed record ShortlistQuality(double Coverage, double RepetitionRate, double NoveltyShare, double Gini)
{
    /// <summary>How many of each replayed list a person actually sees: the shortlist the front page asks for.</summary>
    internal const int Shown = SuggestionContext.DefaultCount;

    /// <summary>How far back a repeat counts as one.</summary>
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

    /// <summary>The Gini coefficient of some counts, including the zeros.</summary>
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
