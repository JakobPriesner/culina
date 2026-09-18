using Domain.Suggestions;

namespace Application.Abstractions;

/// <summary>
/// Scores a household's recipes for one occasion.
/// </summary>
/// <remarks>
/// <para>
/// One port, one implementation, and it earns its place because a real
/// technology boundary is there: the scoring is a SQL statement and the callers
/// are handlers. It is also the seam every later upgrade goes through — a
/// learned repetition interval, stored embeddings, a different similarity — so
/// none of them reaches the callers.
/// </para>
/// <para>
/// <b>The guarantee.</b> A ranker returns exactly
/// <c>min(context.Count, eligible)</c> suggestions, where <i>eligible</i> means
/// the household's recipes minus what the caller explicitly excluded and minus
/// what this person has dismissed. Nothing else removes a candidate: every term
/// is a score and no score is a threshold, so a recipe the household ate
/// yesterday is pushed to the bottom and never out of existence. That is what
/// stops a cold installation, a saturated one, or a strangely-shaped one from
/// quietly returning nothing.
/// </para>
/// </remarks>
public interface ISuggestionRanker
{
    /// <summary>Ranks the household's recipes for this occasion, best first.</summary>
    /// <param name="context">Who is asking, and about what.</param>
    /// <param name="cancellationToken">Cancels the work when the caller goes away.</param>
    Task<IReadOnlyList<ScoredRecipe>> RankAsync(
        SuggestionContext context,
        CancellationToken cancellationToken);
}

/// <summary>One suggestion, with the arithmetic that produced it.</summary>
/// <param name="Recipe">Everything a card needs.</param>
/// <param name="Score">The sum of the terms. Ordering is all it means — never show it.</param>
/// <param name="Terms">
/// Every term that contributed, largest first. A list rather than a scalar so
/// that adding a term changes no caller, and so an explanation can be a fact
/// about the ranking rather than a sentence written afterwards.
/// </param>
/// <param name="Features">
/// The tags and folded ingredient names this recipe has, for the diversity pass.
/// </param>
public sealed record ScoredRecipe(
    RecipeSearchRow Recipe,
    decimal Score,
    IReadOnlyList<ScoreTerm> Terms,
    IReadOnlyList<string> Features);
