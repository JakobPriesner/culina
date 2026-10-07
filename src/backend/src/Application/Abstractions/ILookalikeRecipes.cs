namespace Application.Abstractions;

/// <summary>Finds the recipe a household already has that a new one looks like.</summary>
/// <remarks>
/// Asked before an imported recipe is written; the answer is only ever a question put to a person,
/// who may want two.
/// </remarks>
public interface ILookalikeRecipes
{
    /// <summary>The closest lookalike, or null when nothing here looks like it.</summary>
    Task<Lookalike?> FindAsync(
        Guid householdId,
        Guid userId,
        LookalikeCandidate candidate,
        CancellationToken cancellationToken);
}

/// <summary>What is compared about a recipe that is not written yet.</summary>
/// <param name="Title">Its title.</param>
/// <param name="Ingredients">Its ingredient names, as it writes them.</param>
/// <param name="Concepts">
/// What the lexicon reads it as, with every ancestor, as its search document will.
/// </param>
public sealed record LookalikeCandidate(
    string Title,
    IReadOnlyList<string> Ingredients,
    IReadOnlyList<string> Concepts);

/// <summary>A recipe already here that a new one looks like.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="Title">Its title.</param>
/// <param name="SharedIngredients">How many ingredients the two have in common.</param>
/// <param name="CookCount">
/// How often the person importing has made it, the reason it is worth not doubling.
/// </param>
public sealed record Lookalike(Guid RecipeId, string Title, int SharedIngredients, int CookCount);
