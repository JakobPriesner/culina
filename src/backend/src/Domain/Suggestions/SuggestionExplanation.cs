namespace Domain.Suggestions;

/// <summary>One named contribution to a suggestion's score.</summary>
/// <param name="Reason">Which term.</param>
/// <param name="Contribution">Signed. A negative term ranks and explains nothing.</param>
/// <param name="Subject">The tag, ingredient or person it is about, when it is about one.</param>
public sealed record ScoreTerm(SuggestionReason Reason, decimal Contribution, string? Subject);

/// <summary>
/// Which term, if any, may be shown as the reason a recipe was suggested.
/// </summary>
/// <remarks>
/// <para>
/// A rule rather than a template, and it lives in the domain because it is a
/// promise the product makes rather than a detail of how the ranking is stored:
/// <b>a reason is the term that actually won, or there is no reason.</b>
/// </para>
/// <para>
/// The alternative — writing a plausible sentence for whatever came out on top —
/// is the normal way this feature is built and it is a lie the user eventually
/// catches. Catching it once discredits every other reason on the screen, which
/// makes an unexplained suggestion strictly better than a decorated one.
/// </para>
/// </remarks>
public static class SuggestionExplanation
{
    /// <summary>
    /// The dominant term, or <see cref="SuggestionReason.None"/> when the score
    /// was a committee rather than a decision.
    /// </summary>
    /// <param name="terms">Every contributing term. Order does not matter.</param>
    /// <param name="weights">Carries the share one term must hold to speak for the rest.</param>
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
                // Negative terms rank and never explain. "You had this on
                // Tuesday" is a reason something is NOT being suggested, and
                // printing it on a card that is being suggested is nonsense.
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

        // A term that names something it cannot name — a tag reason with no
        // tag — would render as "You often cook" with a hole in it. Better to
        // say nothing than to say most of a sentence.
        if (NeedsSubject(best.Reason) && string.IsNullOrWhiteSpace(best.Subject))
        {
            return none;
        }

        return best.Contribution >= weights.ReasonDominance * positive ? best : none;
    }

    private static bool NeedsSubject(SuggestionReason reason) =>
        reason is SuggestionReason.Tag or SuggestionReason.Ingredient or SuggestionReason.Household;
}
