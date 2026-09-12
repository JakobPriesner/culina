namespace Contracts.Shopping;

/// <summary>One line on the list.</summary>
public sealed record ItemContract
{
    /// <summary>The line's id.</summary>
    public required Guid ItemId { get; init; }

    /// <summary>What to buy, as somebody wrote it.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// How much, unrounded.
    /// </summary>
    /// <remarks>
    /// Deliberately not rounded here. Summing rounded amounts compounds error,
    /// and how a number is shown is the client's business — see
    /// docs/scaling-rules.md.
    /// </remarks>
    public decimal? Quantity { get; init; }

    /// <summary>In what, or null for a bare count.</summary>
    public string? Unit { get; init; }

    /// <summary>Where in the shop it is found.</summary>
    public required string Section { get; init; }

    /// <summary>Whether it is already in the trolley.</summary>
    public required bool IsChecked { get; init; }

    /// <summary>Whether a person typed it rather than a recipe contributing it.</summary>
    public required bool IsManual { get; init; }
}

/// <summary>A household's shopping list.</summary>
public sealed record Response
{
    /// <summary>The list's id.</summary>
    public required Guid ListId { get; init; }

    /// <summary>What is on it, in shop order.</summary>
    public required IReadOnlyList<ItemContract> Items { get; init; }

    /// <summary>The entity version, for If-Match on a change.</summary>
    public required long Version { get; init; }
}

/// <summary>Something to put on the list.</summary>
public sealed record AddItemRequest
{
    /// <summary>What to buy.</summary>
    public required string Name { get; init; }

    /// <summary>How much, if you know.</summary>
    public decimal? Quantity { get; init; }

    /// <summary>In what.</summary>
    public string? Unit { get; init; }
}

/// <summary>A recipe's ingredients, at a chosen scaling.</summary>
public sealed record AddRecipeRequest
{
    /// <summary>Which recipe.</summary>
    public required Guid RecipeId { get; init; }

    /// <summary>How many it is being made for, so the amounts are the real ones.</summary>
    public required decimal Servings { get; init; }
}

/// <summary>A change to one line.</summary>
public sealed record UpdateItemRequest
{
    /// <summary>Whether it is now in the trolley.</summary>
    public bool? IsChecked { get; init; }

    /// <summary>Where it actually belongs, when the guess was wrong.</summary>
    public string? Section { get; init; }
}
