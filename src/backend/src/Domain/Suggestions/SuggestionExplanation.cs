namespace Domain.Suggestions;

/// <summary>One named contribution to a suggestion's score.</summary>
/// <param name="Reason">Which term.</param>
/// <param name="Contribution">Signed. A negative term ranks and explains nothing.</param>
/// <param name="Subject">The tag, ingredient or person it is about, when it is about one.</param>
public sealed record ScoreTerm(SuggestionReason Reason, decimal Contribution, string? Subject);

/// <summary>Which term, if any, may be shown as the reason a recipe was suggested.</summary>
/// <remarks>
/// A promise the product makes, so it lives in the domain: <b>a reason is the term that actually
/// won, or there is no reason.</b> A plausible sentence for whatever came out on top is a lie users
/// catch, discrediting every other reason.
/// </remarks>
public static class SuggestionExplanation
{
    /// <summary>
    /// The dominant term, or <see cref="SuggestionReason.None"/> when the score was a committee
    /// rather than a decision.
    /// </summary>
    public static ScoreTerm For(IReadOnlyList<ScoreTerm> terms, RankingWeights weights)
    {
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(weights);

        var none = new ScoreTerm(SuggestionReason.None, 0, null);

        var positive = 0m;
        var best = none;

        foreach (var term in terms)
        {
            if (term.Contribution <= 0)
            {
                // Negative terms rank and never explain: "You had this on Tuesday" is no reason to
                // suggest it.
                continue;
            }

            positive += term.Contribution;

            if (term.Contribution > best.Contribution)
            {
                best = term;
            }
        }

        if (positive <= 0)
        {
            return none;
        }

        // A term naming something it cannot (a tag reason with no tag) would render as "You often
        // cook" with a hole in it: say nothing.
        if (NeedsSubject(best.Reason) && string.IsNullOrWhiteSpace(best.Subject))
        {
            return none;
        }

        return best.Contribution >= weights.ReasonDominance * positive ? best : none;
    }

    private static bool NeedsSubject(SuggestionReason reason) =>
        reason is SuggestionReason.Tag or SuggestionReason.Ingredient or SuggestionReason.Household;
}
