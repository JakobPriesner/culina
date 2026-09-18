using Contracts.Recipes;

namespace Contracts.Recipes.Update;

/// <summary>
/// The recipe's complete new state.
/// </summary>
/// <remarks>
/// A replacement, not a patch. The editor holds the whole recipe on screen and
/// saves the whole recipe, so a partial update would only add a second shape to
/// get wrong.
/// </remarks>
public sealed record Request
{
    /// <summary>What to call it.</summary>
    public required string Title { get; init; }

    /// <summary>A short introduction.</summary>
    public string? Description { get; init; }

    /// <summary><c>en</c> or <c>de</c>.</summary>
    public required string Language { get; init; }

    /// <summary>How many it makes.</summary>
    public required decimal YieldAmount { get; init; }

    /// <summary><c>servings</c> or <c>pieces</c>.</summary>
    public required string YieldKind { get; init; }

    /// <summary>
    /// The recipe's own word for what it makes — "Cake", "Gläser", "Blech".
    /// </summary>
    /// <remarks>
    /// Null for nearly every recipe, and a client must then word the yield from
    /// <c>yieldKind</c> in the reader's language. When it is set it replaces
    /// that word and is shown exactly as written — it is one person's noun in
    /// one person's language, so nothing here pluralises or translates it.
    /// </remarks>
    public string? YieldLabel { get; init; }

    /// <summary>Hands-on time.</summary>
    public int? PrepMinutes { get; init; }

    /// <summary>Time in the oven or on the hob.</summary>
    public int? CookMinutes { get; init; }

    /// <summary>Its ingredient groups, in order.</summary>
    public required IReadOnlyList<IngredientGroupContract> Groups { get; init; }

    /// <summary>Its steps, in order.</summary>
    public required IReadOnlyList<StepContract> Steps { get; init; }

    /// <summary>Its tags.</summary>
    public required IReadOnlyList<string> Tags { get; init; }
}
