namespace Contracts.Users;

/// <summary>Figures of an entry or a day; null is unknown, never zero.</summary>
public sealed record DiaryFiguresDetail
{
    /// <summary>Energy in kilocalories.</summary>
    public decimal? Kcal { get; init; }

    /// <summary>Protein in grams.</summary>
    public decimal? Protein { get; init; }

    /// <summary>Carbohydrate in grams.</summary>
    public decimal? Carbohydrate { get; init; }

    /// <summary>Fat in grams.</summary>
    public decimal? Fat { get; init; }

    /// <summary>Saturated fat in grams.</summary>
    public decimal? SaturatedFat { get; init; }

    /// <summary>Sugars in grams.</summary>
    public decimal? Sugars { get; init; }

    /// <summary>Salt in grams.</summary>
    public decimal? Salt { get; init; }
}

/// <summary>One thing a person ate, with the figures it was logged with.</summary>
public sealed record DiaryEntryDetail
{
    /// <summary>The entry's id.</summary>
    public required Guid EntryId { get; init; }

    /// <summary>The day, <c>yyyy-MM-dd</c>.</summary>
    public required DateOnly Date { get; init; }

    /// <summary><c>breakfast</c>, <c>lunch</c>, <c>dinner</c> or <c>snack</c>.</summary>
    public required string Meal { get; init; }

    /// <summary><c>recipe</c>, <c>food</c> (the table or a household's own) or <c>typed</c> (the person's own figures).</summary>
    public required string Source { get; init; }

    /// <summary>The name as it was when logged.</summary>
    public required string Label { get; init; }

    /// <summary>How much: portions of a recipe, or an amount of a food in <c>unit</c>.</summary>
    public required decimal Amount { get; init; }

    /// <summary><c>portion</c>, <c>g</c>, or a typical unit key such as <c>piece</c> or <c>slice</c>.</summary>
    public required string Unit { get; init; }

    /// <summary>The grams the figures were computed from, for a food.</summary>
    public decimal? Grams { get; init; }

    /// <summary>The figures as logged.</summary>
    public required DiaryFiguresDetail Figures { get; init; }

    /// <summary>Something was not counted, so the figures are a lower bound ("mind.").</summary>
    public required bool AtLeast { get; init; }

    /// <summary>A cited typical weight was used, so the figures are an estimate ("≈").</summary>
    public required bool Estimated { get; init; }

    /// <summary>What an estimate rests on: the typical weight's citation.</summary>
    public string? Basis { get; init; }

    /// <summary>The recipe, for a recipe entry; it may have been deleted since.</summary>
    public Guid? RecipeId { get; init; }

    /// <summary>The household whose recipe or own food it was.</summary>
    public Guid? HouseholdId { get; init; }

    /// <summary>The food's code, for a food entry: a BLS code or <c>h:</c> and a household food's id.</summary>
    public string? FoodCode { get; init; }

    /// <summary>The "made it" this portion was logged from.</summary>
    public Guid? CookLogEntryId { get; init; }

    /// <summary>The entry's version.</summary>
    public required long Version { get; init; }
}

/// <summary>One day of a diary: its entries and what they add up to.</summary>
public sealed record DiaryDay
{
    /// <summary>The day.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>The day's entries, by meal and then in the order they were logged.</summary>
    public required IReadOnlyList<DiaryEntryDetail> Entries { get; init; }

    /// <summary>The sum of what is known.</summary>
    public required DiaryFiguresDetail Totals { get; init; }

    /// <summary>Some entry is a lower bound, so the day is one too.</summary>
    public required bool AtLeast { get; init; }

    /// <summary>Some entry is an estimate, so the day is one too.</summary>
    public required bool Estimated { get; init; }

    /// <summary>The kilocalories that come from figures the person typed, for "davon … von dir eingetragen".</summary>
    public required decimal TypedKcal { get; init; }
}
