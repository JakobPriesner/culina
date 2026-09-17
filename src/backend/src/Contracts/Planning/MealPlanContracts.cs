namespace Contracts.Planning;

/// <summary>A week of planned meals.</summary>
/// <remarks>
/// Seven days, always all seven, whether or not anything is planned in them: a
/// week with holes in it is a week the client has to fill in itself, and the
/// empty days are exactly where the screen offers to add something.
/// </remarks>
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

    /// <summary>
    /// How many it is planned for, or null for however many it was written for.
    /// </summary>
    public decimal? Servings { get; init; }

    /// <summary>The servings the recipe itself is written for.</summary>
    public required decimal RecipeServings { get; init; }

    /// <summary><c>breakfast</c>, <c>lunch</c> or <c>dinner</c>.</summary>
    public required string Slot { get; init; }
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
/// <remarks>
/// A change to the entry, not an action on it: which day a meal is on is one
/// of its own fields, so this is a PATCH of the entry rather than a route with
/// a verb in it.
/// </remarks>
public sealed record MoveMealRequest
{
    /// <summary>Which day it moves to. The day it is already on is allowed.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>
    /// <c>breakfast</c>, <c>lunch</c> or <c>dinner</c>. Omit to keep the slot
    /// it already had.
    /// </summary>
    /// <remarks>
    /// Omitting it is what dragging does: dragging a dinner onto Thursday
    /// moves a dinner, and a drag that quietly turned it into a breakfast
    /// would be a drag nobody could aim.
    /// </remarks>
    public string? Slot { get; init; }

    /// <summary>
    /// Which gap in the day it was dropped into, counted from zero. Omit to put
    /// it last.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The gaps of the day as it is on screen right now, with the meal being
    /// moved still in it: <c>0</c> is above everything, and the number of meals
    /// already there is below everything. Counting the gaps rather than the
    /// final index is what makes moving a meal down the day mean the same as
    /// moving one up — a final index has to be adjusted by whether the meal
    /// started above or below its destination, and that adjustment is the
    /// classic place a reorder goes one off.
    /// </para>
    /// <para>
    /// A request rather than an instruction: a day is read in slot order first,
    /// so a breakfast dropped below a dinner lands at the end of the breakfasts
    /// rather than where the finger let go. The whole week comes back, so the
    /// screen never keeps a position the server did not agree to.
    /// </para>
    /// </remarks>
    public int? Position { get; init; }
}
