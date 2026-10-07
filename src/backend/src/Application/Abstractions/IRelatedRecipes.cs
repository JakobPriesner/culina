namespace Application.Abstractions;

/// <summary>Finds the recipes of a household most like one of its own.</summary>
/// <remarks>
/// The recipe's own search document is the query: the concepts it is indexed under, compared with
/// everybody else's. Nothing is indexed or learnt for this.
/// </remarks>
public interface IRelatedRecipes
{
    /// <summary>
    /// One page of the recipes most like this one, closest first, compared across the library
    /// households (its own, then those it inherits from).
    /// </summary>
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
/// <param name="NextCursor">
/// Where this page ended, or null when nothing else is alike enough.
/// </param>
public sealed record RelatedPage(IReadOnlyList<RelatedRecipe> Items, string? NextCursor);

/// <summary>A recipe like another, and what the two have in common.</summary>
/// <param name="Recipe">The recipe, as every list of recipes shows one.</param>
/// <param name="Kinds">
/// Concept keys of what both are (dishes, cuisines, meals, methods, diets), most telling first.
/// </param>
/// <param name="Stuff">Concept keys of what both are made from, most telling first.</param>
/// <param name="KindScore">Share of what the first recipe is that this one is too, 0 to 1.</param>
/// <param name="StuffScore">
/// Share of what the first recipe is made from that this one is too, 0 to 1.
/// </param>
public sealed record RelatedRecipe(
    RecipeSearchRow Recipe,
    IReadOnlyList<string> Kinds,
    IReadOnlyList<string> Stuff,
    double KindScore,
    double StuffScore);
