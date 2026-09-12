namespace Contracts.Recipes.GetAll;

/// <summary>A page of recipes.</summary>
/// <remarks>
/// Always a wrapped object, never a bare array: an array has nowhere to grow
/// paging metadata, and adding it later would be a breaking change.
/// </remarks>
public sealed record Response
{
    /// <summary>The recipes on this page, in the requested order.</summary>
    public required IReadOnlyList<RecipeSummary> Items { get; init; }

    /// <summary>Pass this back as <c>cursor</c> for the next page, or null at the end.</summary>
    public string? NextCursor { get; init; }

    /// <summary>How many recipes match, across all pages.</summary>
    public required int Total { get; init; }
}

/// <summary>
/// A recipe as it appears in a list.
/// </summary>
/// <remarks>
/// Deliberately not the full recipe: a grid of thirty cards has no use for
/// thirty ingredient lists, and sending them would make the first screen the
/// slowest.
/// </remarks>
public sealed record RecipeSummary
{
    /// <summary>The recipe's id.</summary>
    public required Guid RecipeId { get; init; }

    /// <summary>What it is called.</summary>
    public required string Title { get; init; }

    /// <summary>Its hero image, if it has one.</summary>
    public Guid? ImageId { get; init; }

    /// <summary>Prep plus cook, or null when neither is known.</summary>
    public int? TotalMinutes { get; init; }

    /// <summary>How many it makes.</summary>
    public required decimal YieldAmount { get; init; }

    /// <summary><c>servings</c> or <c>pieces</c>.</summary>
    public required string YieldKind { get; init; }

    /// <summary>Its tags.</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary>How many times the caller has made it.</summary>
    public required int CookCount { get; init; }

    /// <summary>When it last changed.</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// How well it fits the ingredients the caller asked about, when they asked
    /// about any.
    /// </summary>
    public IngredientMatch? IngredientMatch { get; init; }
}

/// <summary>
/// How well a recipe fits what you have.
/// </summary>
/// <remarks>
/// Rendered as "uses 3 of 3 · 2 more needed". This is the whole of Culina's
/// answer to "what can I cook?": no pantry to maintain, so nothing to go stale.
/// </remarks>
public sealed record IngredientMatch
{
    /// <summary>How many of the named ingredients this recipe uses.</summary>
    public required int Matched { get; init; }

    /// <summary>How many were named.</summary>
    public required int Requested { get; init; }

    /// <summary>How many other ingredients it still needs.</summary>
    public required int Missing { get; init; }
}
