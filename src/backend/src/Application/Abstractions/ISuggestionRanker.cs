using Domain.Suggestions;

namespace Application.Abstractions;

/// <summary>
/// Scores a household's recipes for one occasion. Returns exactly <c>min(context.Count, eligible)</c> suggestions:
/// scores only reorder, so only explicit exclusions and dismissals remove a candidate.
/// </summary>
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
/// <param name="Terms">Every term that contributed, largest first.</param>
/// <param name="Features">
/// The tags and folded ingredient names this recipe has, for the diversity pass.
/// </param>
public sealed record ScoredRecipe(
    RecipeSearchRow Recipe,
    decimal Score,
    IReadOnlyList<ScoreTerm> Terms,
    IReadOnlyList<string> Features);
