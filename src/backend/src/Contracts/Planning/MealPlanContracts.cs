namespace Contracts.Planning;

/// <summary>A week of planned meals.</summary>
/// <remarks>Always all seven days, empty or not.</remarks>
public sealed record MealPlanResponse
{
    /// <summary>The Monday the week starts on.</summary>
    public required DateOnly From { get; init; }

    /// <summary>The seven days, in order.</summary>
    public required IReadOnlyList<PlannedDay> Days { get; init; }
}

/// <summary>One day of the week.</summary>
public sealed record PlannedDay
{
    /// <summary>Which day.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>What is planned, in the order it was put there.</summary>
    public required IReadOnlyList<PlannedMeal> Meals { get; init; }
}

/// <summary>One planned meal.</summary>
public sealed record PlannedMeal
{
    /// <summary>The entry's id, for taking it off again.</summary>
    public required Guid EntryId { get; init; }

    /// <summary>Which recipe.</summary>
    public required Guid RecipeId { get; init; }

    /// <summary>What it is called, so the card needs no second request.</summary>
    public required string Title { get; init; }

    /// <summary>Its picture, if it has one.</summary>
    public Guid? ImageId { get; init; }

    /// <summary>Hands-on plus cooking, when both are known.</summary>
    public int? TotalMinutes { get; init; }

    /// <summary>How many it is planned for, or null for however many it was written for.</summary>
    public decimal? Servings { get; init; }

    /// <summary>The servings the recipe itself is written for.</summary>
    public required decimal RecipeServings { get; init; }

    /// <summary><c>breakfast</c>, <c>lunch</c> or <c>dinner</c>.</summary>
    public required string Slot { get; init; }

    /// <summary>Whether this meal's ingredients are on the household's shopping list.</summary>
    public required bool IsOnShoppingList { get; init; }
}

/// <summary>Plans a meal.</summary>
public sealed record PlanMealRequest
{
    /// <summary>Which day.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>What to cook.</summary>
    public required Guid RecipeId { get; init; }

    /// <summary>For how many, or omit for however many it was written for.</summary>
    public decimal? Servings { get; init; }

    /// <summary><c>breakfast</c>, <c>lunch</c> or <c>dinner</c>. Defaults to dinner.</summary>
    public string? Slot { get; init; }
}

/// <summary>Moves a planned meal to another day.</summary>
/// <remarks>A PATCH of the entry (its day is one of its fields), not a verb route.</remarks>
public sealed record MoveMealRequest
{
    /// <summary>Which day it moves to. The day it is already on is allowed.</summary>
    public required DateOnly Date { get; init; }

    /// <summary><c>breakfast</c>, <c>lunch</c> or <c>dinner</c>. Omit to keep the current slot, as dragging does.</summary>
    public string? Slot { get; init; }

    /// <summary>
    /// Which gap in the day it was dropped into, counted from zero on the day as shown (the moved meal still in it); omit to
    /// put it last. Only a request: the day is read in slot order, and the whole week comes back.
    /// </summary>
    public int? Position { get; init; }
}
