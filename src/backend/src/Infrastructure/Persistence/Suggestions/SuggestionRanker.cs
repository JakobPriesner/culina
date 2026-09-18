using Application.Abstractions;
using Domain.Suggestions;

namespace Infrastructure.Persistence.Suggestions;

/// <summary>
/// Ranks a household's recipes for one occasion.
/// </summary>
/// <remarks>
/// <para>
/// Two stages, and the split is where it is because of what each can do. The
/// database scores every eligible recipe in one statement — that is arithmetic
/// over rows, and SQL is what that is for. Choosing between the top few, pushing
/// near-identical results apart and deciding what to say about each is a walk
/// over at most a few dozen items, which is C#.
/// </para>
/// <para>
/// Nothing here can shrink the answer below what the database returned: the
/// diversity pass reorders and selects, it never rejects. See
/// <see cref="ISuggestionRanker"/> for the guarantee that depends on it.
/// </para>
/// </remarks>
/// <param name="reader">Runs the scoring query.</param>
/// <param name="weights">What each term is worth.</param>
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
    /// Picks the best, then keeps picking the best of what is left after
    /// discounting whatever resembles an earlier pick.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Five suggestions that are five pasta dishes is the failure a small
    /// library produces most often, and it is not a bug in the scoring — it is
    /// the scoring being faithful to a taste that really is narrow. The
    /// discount is deliberately modest for the same reason: variety that
    /// overrides preference produces a list of things nobody wants, which is
    /// worse than a list of similar things they do.
    /// </para>
    /// <para>
    /// Greedy and O(n·k) over a pool of a few dozen. An optimal selection would
    /// be a different complexity class for a difference nobody could see.
    /// </para>
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
