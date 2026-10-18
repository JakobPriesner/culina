namespace Contracts.Shopping;

/// <summary>One recipe contribution to a shopping-list line.</summary>
public sealed record SourceContract
{
    /// <summary>The recipe that asked for it.</summary>
    public required Guid RecipeId { get; init; }

    /// <summary>What that recipe is called.</summary>
    public required string RecipeTitle { get; init; }

    /// <summary>How much this recipe contributed, unrounded.</summary>
    public decimal? Quantity { get; init; }

    /// <summary>In what, or null for a bare count.</summary>
    public string? Unit { get; init; }

    /// <summary>The planned meal it is for, or null when added directly.</summary>
    public Guid? PlanEntryId { get; init; }

    /// <summary>Which planned day it is for, or null when added directly.</summary>
    public DateOnly? PlannedDate { get; init; }

    /// <summary>Breakfast, lunch or dinner, or null when added directly.</summary>
    public string? PlannedSlot { get; init; }
}

/// <summary>One line on the list.</summary>
public sealed record ItemContract
{
    /// <summary>The line's id.</summary>
    public required Guid ItemId { get; init; }

    /// <summary>What to buy, as somebody wrote it.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// How much, unrounded: summing rounded amounts compounds error, and display is the client's
    /// business.
    /// </summary>
    public decimal? Quantity { get; init; }

    /// <summary>In what, or null for a bare count.</summary>
    public string? Unit { get; init; }

    /// <summary>Where in the shop it is found.</summary>
    public required string Section { get; init; }

    /// <summary>Whether it is already in the trolley.</summary>
    public required bool IsChecked { get; init; }

    /// <summary>Whether a person typed it rather than a recipe contributing it.</summary>
    public required bool IsManual { get; init; }

    /// <summary>Which recipes asked for it, and how much each contributed.</summary>
    public required IReadOnlyList<SourceContract> Sources { get; init; }

    /// <summary>
    /// The part of <c>quantity</c> (in the same unit) that no source accounts for: what somebody typed,
    /// or what a recipe left behind. Null when the sources make up the whole.
    /// </summary>
    public decimal? OtherQuantity { get; init; }

    /// <summary>
    /// Whether another line still to buy is this same thing in a unit (or with no amount) that cannot be added to this one,
    /// so the two stay apart instead of being summed wrongly.
    /// </summary>
    public bool IsListedSeparately { get; init; }
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

    /// <summary>The recipe's ingredients to leave off: already in the cupboard, or already on the list.</summary>
    public IReadOnlyList<Guid>? ExceptIngredientIds { get; init; }
}

/// <summary>A planned week's shopping.</summary>
public sealed record AddPlannedMealsRequest
{
    /// <summary>The day the week starts on, as the plan was read.</summary>
    public required DateOnly From { get; init; }

    /// <summary>Only this planned meal of the week, as when it is planned after the shop; null for all of them.</summary>
    public Guid? EntryId { get; init; }

    /// <summary>With <see cref="EntryId"/>: that recipe's ingredients to leave off, already in the cupboard or on the list.</summary>
    public IReadOnlyList<Guid>? ExceptIngredientIds { get; init; }
}

/// <summary>A change to one line.</summary>
public sealed record UpdateItemRequest
{
    /// <summary>Whether it is now in the trolley.</summary>
    public bool? IsChecked { get; init; }

    /// <summary>Where it actually belongs, when the guess was wrong.</summary>
    public string? Section { get; init; }

    /// <summary>How much to buy now, in the line's own unit: a person correcting the sum.</summary>
    public decimal? Quantity { get; init; }
}
