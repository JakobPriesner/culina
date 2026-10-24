using Contracts.Users;
using Domain.Diary;
using Domain.Shared;

namespace Application.Users;

/// <summary>Turns diary entries and settings into what the API sends.</summary>
internal static class DiaryMappings
{
    internal static DiaryEntryDetail ToDetail(this DiaryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new DiaryEntryDetail
        {
            EntryId = entry.Id,
            Date = entry.Date,
            Meal = DiaryCodes.Of(entry.Meal),
            Source = DiaryCodes.Of(entry.Source),
            Label = entry.Label,
            Amount = entry.Amount.Value,
            Unit = entry.Amount.Unit,
            Grams = entry.Amount.Grams,
            Figures = entry.Figures.ToDetail(),
            AtLeast = entry.Trust.AtLeast,
            Estimated = entry.Trust.Estimated,
            Basis = entry.Trust.Basis,
            RecipeId = entry.Link.RecipeId,
            HouseholdId = entry.Link.HouseholdId,
            FoodCode = entry.Link.FoodCode,
            CookLogEntryId = entry.Link.CookLogEntryId,
            Version = entry.Version
        };
    }

    internal static DiaryFiguresDetail ToDetail(this DiaryFigures figures)
    {
        ArgumentNullException.ThrowIfNull(figures);

        return new DiaryFiguresDetail
        {
            Kcal = figures.Kcal,
            Protein = figures.Protein,
            Carbohydrate = figures.Carbohydrate,
            Fat = figures.Fat,
            SaturatedFat = figures.SaturatedFat,
            Sugars = figures.Sugars,
            Salt = figures.Salt
        };
    }

    /// <summary>One day: its entries by meal, then in the order logged, and what they add up to.</summary>
    internal static DiaryDay ToDay(DateOnly date, IReadOnlyCollection<DiaryEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var ordered = entries.OrderBy(entry => entry.Meal).ThenBy(entry => entry.CreatedAt).ToList();

        return new DiaryDay
        {
            Date = date,
            Entries = [.. ordered.Select(entry => entry.ToDetail())],
            Totals = DiaryFigures.Sum([.. ordered.Select(entry => entry.Figures)]).ToDetail(),
            // An entry whose energy is unknown leaves the day's energy a lower bound too.
            AtLeast = ordered.Any(entry => entry.Trust.AtLeast || entry.Figures.Kcal is null),
            Estimated = ordered.Any(entry => entry.Trust.Estimated),
            TypedKcal = Amounts.Round(ordered
                .Where(entry => entry.Source == DiarySource.Typed)
                .Sum(entry => entry.Figures.Kcal ?? 0))
        };
    }

    internal static string HeadlineCode(DiaryHeadline headline) =>
        headline == DiaryHeadline.Remaining ? "remaining" : "eaten";

    internal static DiaryHeadline? ToHeadline(string? code) => code switch
    {
        null or "" or "eaten" => DiaryHeadline.Eaten,
        "remaining" => DiaryHeadline.Remaining,
        _ => null
    };
}
