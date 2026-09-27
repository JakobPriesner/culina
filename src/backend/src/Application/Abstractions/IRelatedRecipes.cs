namespace Application.Abstractions;

/// <summary>
/// Finds the recipes of a household most like one of its own.
/// </summary>
/// <remarks>
/// The recipe's own search document is the query: the concepts it is indexed
/// under, compared with everybody else's. Nothing is indexed for this, and
/// nothing about it is learnt.
/// </remarks>
public interface IRelatedRecipes
{
    /// <summary>One page of the recipes most like this one, the closest first.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="library">
    /// The households whose recipes it is compared with: its own, then those
    /// its own inherits from.
    /// </param>
    /// <param name="userId">Who is reading, whose cooking the summaries count.</param>
    /// <param name="cursor">Where the previous page ended, or null for the first.</param>
    /// <param name="limit">How many at most.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<RelatedPage> FindAsync(
        Guid recipeId,
        IReadOnlyList<Guid> library,
        Guid userId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken);
}

/// <summary>One page of related recipes.</summary>
/// <param name="Items">The closest first.</param>
/// <param name="NextCursor">Where this page ended, or null when nothing else is alike enough.</param>
public sealed record RelatedPage(IReadOnlyList<RelatedRecipe> Items, string? NextCursor);

/// <summary>A recipe like another, and what the two have in common.</summary>
/// <param name="Recipe">The recipe, as every list of recipes shows one.</param>
/// <param name="Kinds">
/// Concept keys of what both of them are — dishes, cuisines, meals, methods,
/// diets, characters — the most telling first.
/// </param>
/// <param name="Stuff">
/// Concept keys of what both of them are made from, the most telling first.
/// </param>
/// <param name="KindScore">How much of what the first recipe is, this one is too, from 0 to 1.</param>
/// <param name="StuffScore">How much of what the first recipe is made from, this one is too, from 0 to 1.</param>
public sealed record RelatedRecipe(
    RecipeSearchRow Recipe,
    IReadOnlyList<string> Kinds,
    IReadOnlyList<string> Stuff,
    double KindScore,
    double StuffScore);
