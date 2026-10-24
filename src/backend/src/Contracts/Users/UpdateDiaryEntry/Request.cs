namespace Contracts.Users.UpdateDiaryEntry;

/// <summary>
/// A change to an entry. A new day or meal moves it and keeps its figures; a new amount (portions of a recipe, or an
/// amount and unit of a food) looks the figures up again. Left out, a field stays as it is. An entry of the person's
/// own figures is corrected by sending them again: a name or kilocalories left out stay, a macro left out becomes
/// unknown.
/// </summary>
public sealed record Request
{
    /// <summary>Another day.</summary>
    public DateOnly? Date { get; init; }

    /// <summary>Another meal.</summary>
    public string? Meal { get; init; }

    /// <summary>Another number of portions of the recipe.</summary>
    public decimal? Portions { get; init; }

    /// <summary>Another amount of the food.</summary>
    public decimal? Amount { get; init; }

    /// <summary>Another unit for the food's amount.</summary>
    public string? Unit { get; init; }

    /// <summary>Own figures: what it was.</summary>
    public string? Label { get; init; }

    /// <summary>Own figures: kilocalories.</summary>
    public decimal? Kcal { get; init; }

    /// <summary>Own figures: protein in grams, or unknown.</summary>
    public decimal? Protein { get; init; }

    /// <summary>Own figures: carbohydrate in grams, or unknown.</summary>
    public decimal? Carbohydrate { get; init; }

    /// <summary>Own figures: fat in grams, or unknown.</summary>
    public decimal? Fat { get; init; }
}
