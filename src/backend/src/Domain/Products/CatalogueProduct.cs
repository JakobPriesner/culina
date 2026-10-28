using Domain.Nutrition;

namespace Domain.Products;

/// <summary>
/// A packaged product as Open Food Facts describes it: crowd-sourced, so shown with its source and its doubts, and
/// only ever kept by a household when a person confirms it (<c>docs/app-structure-design.md</c> §6.3).
/// </summary>
/// <param name="Gtin">The barcode.</param>
/// <param name="Name">The product's name, German where it has one: "Skyr Natur".</param>
/// <param name="Brand">Its brand, the first if several: "Arla".</param>
/// <param name="Quantity">What the pack says it holds, as printed: "450 g".</param>
/// <param name="PackGrams">The pack's contents in grams (or millilitres), when Open Food Facts gives them as a number.</param>
/// <param name="PackUnit">Which of the two <paramref name="PackGrams"/> is: <c>g</c> or <c>ml</c>.</param>
/// <param name="ServingGrams">One serving in grams (or millilitres), when the label names one.</param>
/// <param name="Per100Grams">The label's values per 100 g (or 100 ml); null is what nobody entered, never zero.</param>
public sealed record CatalogueProduct(
    Gtin Gtin,
    string Name,
    string? Brand,
    string? Quantity,
    decimal? PackGrams,
    string? PackUnit,
    decimal? ServingGrams,
    Nutrients Per100Grams)
{
    /// <summary>Energy and macros may disagree by this share before the values are doubted: EU label tolerances are about ±20 %.</summary>
    private const decimal Tolerance = 0.2m;

    /// <summary>Below this many kilocalories a disagreement is rounding, not an error (a drink, a spice).</summary>
    private const decimal Slack = 20m;

    /// <summary>A kilocalorie in kilojoules, as the EU label regulation counts it.</summary>
    private const decimal KilojoulesPerKilocalorie = 4.184m;

    /// <summary>
    /// What to doubt before taking the values over, the mistakes crowd-sourced labels are known for: kilojoules typed as
    /// kilocalories, per-serving values typed as per 100 g, and gaps. Missing values are named, never read as zero.
    /// </summary>
    public IReadOnlyList<string> Doubts()
    {
        var values = Per100Grams;
        var doubts = new List<string>();

        if (values.EnergyKcal is not { } kcal)
        {
            doubts.Add(CatalogueDoubts.NoEnergy);

            return doubts;
        }

        if (values is { Fat: { } fat, Carbohydrate: { } carbohydrate, Protein: { } protein })
        {
            var counted = 9m * fat + 4m * carbohydrate + 4m * protein;

            if (Math.Abs(kcal - counted) > Math.Max(Slack, Tolerance * Math.Max(kcal, counted)))
            {
                doubts.Add(CatalogueDoubts.EnergyMismatch);
            }
        }
        else
        {
            doubts.Add(CatalogueDoubts.MacrosMissing);
        }

        if (values.EnergyKj is { } kj && Math.Abs(kj - kcal * KilojoulesPerKilocalorie) > Math.Max(Slack, Tolerance * kj))
        {
            doubts.Add(CatalogueDoubts.UnitMismatch);
        }

        if (kcal > 900m || new[] { values.Fat, values.Carbohydrate, values.Protein, values.Salt }.Any(grams => grams > 100m))
        {
            doubts.Add(CatalogueDoubts.Impossible);
        }

        return doubts;
    }
}

/// <summary>The doubts <see cref="CatalogueProduct.Doubts"/> can raise, as the client reads them.</summary>
public static class CatalogueDoubts
{
    /// <summary>No kilocalories: nothing to count, so it cannot be taken over as it is.</summary>
    public const string NoEnergy = "noEnergy";

    /// <summary>Fat, carbohydrate or protein are missing; what is there is still right, but the day's macros will be short.</summary>
    public const string MacrosMissing = "macrosMissing";

    /// <summary>The kilocalories do not match 9 × fat + 4 × carbohydrate + 4 × protein: often per-serving values.</summary>
    public const string EnergyMismatch = "energyMismatch";

    /// <summary>Kilojoules and kilocalories disagree: often one typed into the other's field.</summary>
    public const string UnitMismatch = "unitMismatch";

    /// <summary>More than 100 g of something in 100 g, or more energy than pure fat.</summary>
    public const string Impossible = "impossible";
}
