using Domain.Nutrition;
using Domain.Shared;

namespace Domain.Diary;

/// <summary>What one entry, or one day, adds up to; null is unknown, never zero.</summary>
/// <param name="Kcal">Energy in kilocalories.</param>
/// <param name="Protein">Protein in grams.</param>
/// <param name="Carbohydrate">Carbohydrate in grams.</param>
/// <param name="Fat">Fat in grams.</param>
/// <param name="SaturatedFat">Saturated fat in grams.</param>
/// <param name="Sugars">Sugars in grams.</param>
/// <param name="Salt">Salt in grams.</param>
public sealed record DiaryFigures(
    decimal? Kcal,
    decimal? Protein,
    decimal? Carbohydrate,
    decimal? Fat,
    decimal? SaturatedFat,
    decimal? Sugars,
    decimal? Salt)
{
    /// <summary>Nothing known.</summary>
    public static DiaryFigures Unknown { get; } = new(null, null, null, null, null, null, null);

    /// <summary>A food's label values scaled to an amount: per 100 g, times grams over 100.</summary>
    /// <param name="per100Grams">The food's values per 100 g.</param>
    /// <param name="grams">How much was eaten.</param>
    public static DiaryFigures Of(Nutrients per100Grams, decimal grams)
    {
        ArgumentNullException.ThrowIfNull(per100Grams);

        return Scaled(per100Grams, grams / 100m);
    }

    /// <summary>Label values times a factor, such as one portion's values times the portions eaten.</summary>
    /// <param name="values">The values of one unit.</param>
    /// <param name="factor">How many units.</param>
    public static DiaryFigures OfUnits(Nutrients values, decimal factor)
    {
        ArgumentNullException.ThrowIfNull(values);

        return Scaled(values, factor);
    }

    /// <summary>The sum of entries' figures: a value is known when every entry knows it, and a lower bound otherwise.</summary>
    /// <param name="parts">The entries' figures.</param>
    public static DiaryFigures Sum(IReadOnlyCollection<DiaryFigures> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        // Unknown parts add nothing; whether the sum is complete is the totals' AtLeast to say, not a null here.
        static decimal? Add(IEnumerable<decimal?> values)
        {
            var known = values.Where(value => value.HasValue).Select(value => value!.Value).ToList();

            return known.Count == 0 ? null : Amounts.Round(known.Sum());
        }

        return new DiaryFigures(
            Add(parts.Select(part => part.Kcal)),
            Add(parts.Select(part => part.Protein)),
            Add(parts.Select(part => part.Carbohydrate)),
            Add(parts.Select(part => part.Fat)),
            Add(parts.Select(part => part.SaturatedFat)),
            Add(parts.Select(part => part.Sugars)),
            Add(parts.Select(part => part.Salt)));
    }

    private static DiaryFigures Scaled(Nutrients values, decimal factor)
    {
        decimal? Scale(decimal? value) => value is { } known ? Amounts.Round(known * factor) : null;

        return new DiaryFigures(
            Scale(values.EnergyKcal),
            Scale(values.Protein),
            Scale(values.Carbohydrate),
            Scale(values.Fat),
            Scale(values.SaturatedFat),
            Scale(values.Sugars),
            Scale(values.Salt));
    }
}
