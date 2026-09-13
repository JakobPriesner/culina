namespace Contracts.Recipes.GetIngredients;

/// <summary>An ingredient a recipe could call for.</summary>
/// <param name="Name">What to write, in the language that was asked for.</param>
/// <param name="Section">Where in a shop it is found.</param>
/// <param name="Own">Whether this household has written it before.</param>
public sealed record IngredientSuggestion(string Name, string Section, bool Own);

/// <summary>What a kitchen could be cooking with.</summary>
/// <remarks>
/// The household's own words first, then the seeded ones. After a few recipes
/// a kitchen's own vocabulary is the better suggestion — it is how these people
/// actually talk — and the seeded list is the floor rather than the limit.
/// Nothing has to be chosen from either.
/// </remarks>
public sealed record Response
{
    /// <summary>The suggestions, best first.</summary>
    public required IReadOnlyList<IngredientSuggestion> Items { get; init; }
}
