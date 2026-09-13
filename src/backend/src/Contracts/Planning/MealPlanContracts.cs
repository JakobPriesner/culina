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
