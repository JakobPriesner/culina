using Application.Abstractions;
using Domain.Suggestions;

namespace Infrastructure.Persistence.Suggestions;

/// <summary>Ranks a household's recipes for one occasion.</summary>
/// <remarks>
/// The database scores every eligible recipe in one statement (arithmetic over rows); choosing,
/// pushing near-duplicates apart and explaining is a walk over a few dozen items, which is C#.
/// Nothing here shrinks the answer below what the database returned: the diversity pass reorders
/// and selects, never rejects (see <see cref="ISuggestionRanker"/>).
/// </remarks>
internal sealed class SuggestionRanker(SuggestionReader reader, RankingWeights weights)
    : ISuggestionRanker
{
    public async Task<IReadOnlyList<ScoredRecipe>> RankAsync(
        SuggestionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var scored = await reader.ScoreAsync(context, cancellationToken).ConfigureAwait(false);

        return context.Diversify
            ? Diversified(scored, context.Count, weights.DiversityPenalty)
            : [.. scored.Take(context.Count)];
    }

    /// <summary>
    /// Picks the best, then keeps picking the best of what is left after discounting whatever
    /// resembles an earlier pick.
    /// </summary>
    /// <remarks>
    /// Greedy, O(n·k) over a few dozen. The discount is modest: variety that overrides preference
    /// is worse than a list of similar things they do want.
    /// </remarks>
    private static List<ScoredRecipe> Diversified(
        IReadOnlyList<ScoredRecipe> candidates,
        int count,
        decimal penalty)
    {
        var remaining = candidates.ToList();
        List<ScoredRecipe> chosen = [];

        while (chosen.Count < count && remaining.Count > 0)
        {
            var best = 0;
            var bestScore = decimal.MinValue;

            for (var index = 0; index < remaining.Count; index++)
            {
                var adjusted = remaining[index].Score - (penalty * Resemblance(remaining[index], chosen));

                if (adjusted > bestScore)
                {
                    best = index;
                    bestScore = adjusted;
                }
            }

            chosen.Add(remaining[best]);
            remaining.RemoveAt(best);
        }

        return chosen;
    }

    /// <summary>How much a candidate looks like the most similar thing already chosen.</summary>
    private static decimal Resemblance(ScoredRecipe candidate, List<ScoredRecipe> chosen)
    {
        var worst = 0m;

        foreach (var picked in chosen)
        {
            var overlap = Jaccard(candidate.Features, picked.Features);

            if (overlap > worst)
            {
                worst = overlap;
            }
        }

        return worst;
    }

    /// <summary>Shared features over combined features, on two small sets.</summary>
    private static decimal Jaccard(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0;
        }

        var shared = 0;

        foreach (var feature in left)
        {
            if (right.Contains(feature, StringComparer.Ordinal))
            {
                shared++;
            }
        }

        return (decimal)shared / (left.Count + right.Count - shared);
    }
}
