using Domain.Shared;

namespace Domain.Diary;

/// <summary>What one person ate: a date, a meal, what it was and the figures it was logged with.</summary>
/// <remarks>
/// The figures are a snapshot: a recipe edited or binned later does not change what was eaten on Monday. An entry is
/// looked up (a food by weight, a fully counted recipe), estimated (a cited typical unit, a recipe with typical weights)
/// or typed by the person, and says which.
/// </remarks>
public sealed class DiaryEntry
{
    /// <summary>The longest name an entry may carry.</summary>
    public const int MaxLabelLength = 200;

    /// <summary>The most portions of a recipe one entry may hold.</summary>
    public const decimal MaxPortions = 20m;

    /// <summary>The most grams of a food one entry may hold.</summary>
    public const decimal MaxGrams = 5000m;

    /// <summary>The most kilocalories a typed entry may claim.</summary>
    public const decimal MaxTypedKcal = 10000m;

    private DiaryEntry(
        Guid id,
        Guid userId,
        DateOnly date,
        DiaryMeal meal,
        DiarySource source,
        DiaryLink link,
        DiaryAmount amount,
        string label,
        DiaryFigures figures,
        DiaryTrust trust,
        int nutritionVersion,
        DateTimeOffset createdAt,
        long version)
    {
        Id = id;
        UserId = userId;
        Date = date;
        Meal = meal;
        Source = source;
        Link = link;
        Amount = amount;
        Label = label;
        Figures = figures;
        Trust = trust;
        NutritionVersion = nutritionVersion;
        CreatedAt = createdAt;
        Version = version;
    }

    /// <summary>The entry's id.</summary>
    public Guid Id { get; }

    /// <summary>Who ate it; a diary is never a household's.</summary>
    public Guid UserId { get; }

    /// <summary>The day, as a date: travelling must not move a breakfast.</summary>
    public DateOnly Date { get; }

    /// <summary>The meal.</summary>
    public DiaryMeal Meal { get; }

    /// <summary>Where the figures come from.</summary>
    public DiarySource Source { get; }

    /// <summary>What the entry points at, if anything.</summary>
    public DiaryLink Link { get; }

    /// <summary>How much was eaten.</summary>
    public DiaryAmount Amount { get; }

    /// <summary>The name as it was when logged.</summary>
    public string Label { get; }

    /// <summary>The figures as they were when logged.</summary>
    public DiaryFigures Figures { get; }

    /// <summary>How far the figures can be trusted.</summary>
    public DiaryTrust Trust { get; }

    /// <summary>The nutrition data version the figures were computed with.</summary>
    public int NutritionVersion { get; }

    /// <summary>When it was logged.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>The entry's version, raised by every change.</summary>
    public long Version { get; }

    /// <summary>Logs a portion of a recipe.</summary>
    /// <param name="userId">Who ate it.</param>
    /// <param name="date">The day.</param>
    /// <param name="meal">The meal.</param>
    /// <param name="link">The recipe, its household and the cook-log entry it came from.</param>
    /// <param name="portions">How many portions (or pieces).</param>
    /// <param name="label">The recipe's title.</param>
    /// <param name="figures">The figures of the portions eaten.</param>
    /// <param name="trust">Whether the figures are complete and whether they rest on typical weights.</param>
    /// <param name="nutritionVersion">The nutrition data version.</param>
    /// <param name="now">When.</param>
    public static Result<DiaryEntry> OfRecipe(
        Guid userId,
        DateOnly date,
        DiaryMeal meal,
        DiaryLink link,
        decimal portions,
        string label,
        DiaryFigures figures,
        DiaryTrust trust,
        int nutritionVersion,
        DateTimeOffset now)
    {
        var rounded = Amounts.Round(portions);

        return rounded is <= 0 or > MaxPortions
            ? DiaryErrors.InvalidAmount
            : Create(
                CulinaId.New(),
                userId,
                date,
                meal,
                DiarySource.Recipe,
                link,
                new DiaryAmount(rounded, DiaryAmount.Portion, null),
                label,
                figures,
                trust,
                nutritionVersion,
                now,
                version: 1);
    }

    /// <summary>Logs an amount of a food.</summary>
    /// <param name="userId">Who ate it.</param>
    /// <param name="date">The day.</param>
    /// <param name="meal">The meal.</param>
    /// <param name="link">The food's code and, for a household's own, its household.</param>
    /// <param name="amount">How much, in which unit, and the grams it came to.</param>
    /// <param name="label">The food's name.</param>
    /// <param name="figures">The figures of that amount.</param>
    /// <param name="trust">Estimated when a typical unit weight turned the amount into grams.</param>
    /// <param name="nutritionVersion">The nutrition data version.</param>
    /// <param name="now">When.</param>
    public static Result<DiaryEntry> OfFood(
        Guid userId,
        DateOnly date,
        DiaryMeal meal,
        DiaryLink link,
        DiaryAmount amount,
        string label,
        DiaryFigures figures,
        DiaryTrust trust,
        int nutritionVersion,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(amount);

        return amount.Value <= 0 || amount.Grams is not ({ } grams and > 0 and <= MaxGrams)
            ? DiaryErrors.InvalidAmount
            : Create(
                CulinaId.New(),
                userId,
                date,
                meal,
                DiarySource.Food,
                link,
                amount with { Value = Amounts.Round(amount.Value), Grams = Amounts.Round(grams) },
                label,
                figures,
                trust,
                nutritionVersion,
                now,
                version: 1);
    }

    /// <summary>Logs figures the person typed; unknown macros stay unknown.</summary>
    /// <param name="userId">Who ate it.</param>
    /// <param name="date">The day.</param>
    /// <param name="meal">The meal.</param>
    /// <param name="label">What it was, in their words.</param>
    /// <param name="figures">What they typed; energy is required.</param>
    /// <param name="nutritionVersion">The nutrition data version.</param>
    /// <param name="now">When.</param>
    public static Result<DiaryEntry> Typed(
        Guid userId,
        DateOnly date,
        DiaryMeal meal,
        string? label,
        DiaryFigures figures,
        int nutritionVersion,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(figures);

        static bool Plausible(decimal? grams) => grams is null or (>= 0 and <= MaxGrams);

        return figures.Kcal is not (>= 0 and <= MaxTypedKcal)
               || !Plausible(figures.Protein)
               || !Plausible(figures.Carbohydrate)
               || !Plausible(figures.Fat)
               || string.IsNullOrWhiteSpace(label)
            ? DiaryErrors.Implausible
            : Create(
                CulinaId.New(),
                userId,
                date,
                meal,
                DiarySource.Typed,
                DiaryLink.None,
                new DiaryAmount(1, DiaryAmount.Portion, null),
                label,
                figures with
                {
                    Kcal = Amounts.Round(figures.Kcal),
                    Protein = Amounts.Round(figures.Protein),
                    Carbohydrate = Amounts.Round(figures.Carbohydrate),
                    Fat = Amounts.Round(figures.Fat),
                    SaturatedFat = null,
                    Sugars = null,
                    Salt = null
                },
                DiaryTrust.Typed,
                nutritionVersion,
                now,
                version: 1);
    }

    /// <summary>The same entry on another day or at another meal; the figures stay as logged.</summary>
    /// <param name="date">The day.</param>
    /// <param name="meal">The meal.</param>
    public DiaryEntry MovedTo(DateOnly date, DiaryMeal meal) =>
        new(Id, UserId, date, meal, Source, Link, Amount, Label, Figures, Trust, NutritionVersion, CreatedAt, Version + 1);

    /// <summary>The same entry with what a fresh lookup of a new amount came to; it keeps its id, day and meal.</summary>
    /// <param name="relogged">The entry a new log of the new amount produced.</param>
    public DiaryEntry Replacing(DiaryEntry relogged)
    {
        ArgumentNullException.ThrowIfNull(relogged);

        return new DiaryEntry(
            Id,
            UserId,
            Date,
            Meal,
            relogged.Source,
            relogged.Link with { CookLogEntryId = Link.CookLogEntryId },
            relogged.Amount,
            relogged.Label,
            relogged.Figures,
            relogged.Trust,
            relogged.NutritionVersion,
            CreatedAt,
            Version + 1);
    }

    /// <summary>Rebuilds an entry from storage.</summary>
    /// <param name="id">Its id.</param>
    /// <param name="userId">Whose.</param>
    /// <param name="date">The day.</param>
    /// <param name="meal">The meal.</param>
    /// <param name="source">Where the figures come from.</param>
    /// <param name="link">What it points at.</param>
    /// <param name="amount">How much.</param>
    /// <param name="label">The name.</param>
    /// <param name="figures">The figures.</param>
    /// <param name="trust">How far they can be trusted.</param>
    /// <param name="nutritionVersion">The data version.</param>
    /// <param name="createdAt">When it was logged.</param>
    /// <param name="version">Its version.</param>
    public static DiaryEntry Restore(
        Guid id,
        Guid userId,
        DateOnly date,
        DiaryMeal meal,
        DiarySource source,
        DiaryLink link,
        DiaryAmount amount,
        string label,
        DiaryFigures figures,
        DiaryTrust trust,
        int nutritionVersion,
        DateTimeOffset createdAt,
        long version) =>
        new(id, userId, date, meal, source, link, amount, label, figures, trust, nutritionVersion, createdAt, version);

    private static Result<DiaryEntry> Create(
        Guid id,
        Guid userId,
        DateOnly date,
        DiaryMeal meal,
        DiarySource source,
        DiaryLink link,
        DiaryAmount amount,
        string label,
        DiaryFigures figures,
        DiaryTrust trust,
        int nutritionVersion,
        DateTimeOffset now,
        long version)
    {
        var name = label.Trim();

        if (name.Length > MaxLabelLength)
        {
            name = name[..MaxLabelLength].TrimEnd();
        }

        return DiaryDates.IsWithin(date)
            ? new DiaryEntry(id, userId, date, meal, source, link, amount, name, figures, trust, nutritionVersion, now, version)
            : DiaryErrors.InvalidDates;
    }
}

/// <summary>What an entry points at; soft links that may outlive what they name.</summary>
/// <param name="RecipeId">The recipe, for a recipe entry.</param>
/// <param name="HouseholdId">The household whose recipe or own food it was.</param>
/// <param name="FoodCode">The food's code, for a food entry.</param>
/// <param name="CookLogEntryId">The "made it" this portion was logged from, so undoing it can offer to take the portion too.</param>
public sealed record DiaryLink(Guid? RecipeId, Guid? HouseholdId, string? FoodCode, Guid? CookLogEntryId)
{
    /// <summary>Points at nothing: a typed entry.</summary>
    public static DiaryLink None { get; } = new(null, null, null, null);
}

/// <summary>How much was eaten.</summary>
/// <param name="Value">The amount, in <paramref name="Unit"/>.</param>
/// <param name="Unit"><c>portion</c> for a recipe, <c>g</c> or a typical unit key (<c>piece</c>, <c>slice</c>) for a food.</param>
/// <param name="Grams">The weight the figures were computed from, for a food.</param>
public sealed record DiaryAmount(decimal Value, string Unit, decimal? Grams)
{
    /// <summary>The unit of a recipe entry and of a typed one.</summary>
    public const string Portion = "portion";

    /// <summary>Grams, the unit a food entry is exact in.</summary>
    public const string Gram = "g";
}

/// <summary>How far an entry's figures can be trusted, and why.</summary>
/// <param name="AtLeast">Something was not counted, so the figures are a lower bound.</param>
/// <param name="Estimated">A cited typical weight was used, so the figures are an estimate.</param>
/// <param name="Basis">What an estimate rests on: the typical weight's citation.</param>
public sealed record DiaryTrust(bool AtLeast, bool Estimated, string? Basis)
{
    /// <summary>A lookup that counted everything exactly.</summary>
    public static DiaryTrust Exact { get; } = new(false, false, null);

    /// <summary>The person's own figures: not a lookup, so neither flag applies; the source says it.</summary>
    public static DiaryTrust Typed { get; } = new(false, false, null);
}

/// <summary>The days a diary can hold.</summary>
public static class DiaryDates
{
    /// <summary>The first day an entry may have.</summary>
    public static readonly DateOnly First = new(2000, 1, 1);

    /// <summary>The last day an entry may have.</summary>
    public static readonly DateOnly Last = new(2100, 12, 31);

    /// <summary>The most days one read may cover.</summary>
    public const int MaxRangeDays = 62;

    /// <summary>Whether the day is one the diary can hold.</summary>
    public static bool IsWithin(DateOnly date) => date >= First && date <= Last;
}
