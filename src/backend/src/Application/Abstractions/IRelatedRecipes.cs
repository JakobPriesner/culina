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
    /// <summary>The recipes most like this one, the closest first.</summary>
    /// <param name="recipeId">Which recipe.</param>
    /// <param name="householdId">Whose library it is compared with.</param>
    /// <param name="userId">Who is reading, whose cooking the summaries count.</param>
    /// <param name="limit">How many at most.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    Task<IReadOnlyList<RelatedRecipe>> FindAsync(
        Guid recipeId,
        Guid householdId,
        Guid userId,
        int limit,
        CancellationToken cancellationToken);
}

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
