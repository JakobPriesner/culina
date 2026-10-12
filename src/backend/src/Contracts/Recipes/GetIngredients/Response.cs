namespace Contracts.Recipes.GetIngredients;

/// <summary>An ingredient a recipe could call for.</summary>
/// <param name="Name">What to write, in the language that was asked for.</param>
/// <param name="Section">Where in a shop it is found.</param>
/// <param name="Own">Whether this household has written it before.</param>
/// <param name="Equivalent">
/// Whether this is the household's own name for what was typed, though spelt otherwise: a name it
/// merged the typed one into, or one that looks like it ("Hähnchenbrust" for "Hühnchenbrustfilet").
/// At most one suggestion is, and it comes first.
/// </param>
public sealed record IngredientSuggestion(string Name, string Section, bool Own, bool Equivalent);

/// <summary>How a typed name reads, in the words it was typed in.</summary>
/// <param name="Understood">
/// The thing and its kinds, which is what lines of it are merged and looked up as: "Kokosmilch, light" for
/// "Kokosmilch light", "Zwiebeln" for "große Zwiebeln".
/// </param>
/// <param name="Name">
/// The name without what is done to it in the kitchen: "Zwiebeln" for "gehackte Zwiebeln". Null when nothing is.
/// </param>
/// <param name="Note">What is done to it, which belongs in the note: "gehackt". Null when nothing is.</param>
public sealed record TypedName(string Understood, string? Name, string? Note);

/// <summary>What a kitchen could be cooking with.</summary>
/// <remarks>
/// The household's own words first, then the seeded list, which is a floor rather than a limit;
/// nothing has to be chosen from either.
/// </remarks>
public sealed record Response
{
    /// <summary>The suggestions, best first.</summary>
    public required IReadOnlyList<IngredientSuggestion> Items { get; init; }

    /// <summary>How what was typed reads; null when nothing was.</summary>
    public TypedName? Typed { get; init; }
}
