using System.Globalization;
using Domain.Shared;

namespace Domain.Nutrition;

/// <summary>
/// Foods a household typed from a package's label, for what the Bundeslebensmittelschlüssel does not have ("Kokosmilch
/// light"). Cited as "from the package" and chosen for an ingredient name like any food.
/// </summary>
/// <remarks>
/// Its code is <see cref="Prefix"/> and the id, which no BLS code (a letter and six digits) can be, so a correction
/// stores either kind of food the same way.
/// </remarks>
public static class HouseholdFoods
{
    /// <summary>What every household food's code begins with.</summary>
    public const string Prefix = "h:";

    /// <summary>The longest name a food may have, as for an ingredient.</summary>
    public const int LongestName = 120;

    /// <summary>A kilocalorie in kilojoules, as the EU label regulation counts it.</summary>
    private const decimal KilojoulesPerKilocalorie = 4.184m;

    /// <summary>The most energy 100 g can hold: pure fat is 900 kcal.</summary>
    private const decimal MostKcal = 900m;

    /// <summary>The most of any one nutrient 100 g can hold.</summary>
    private const decimal MostGrams = 100m;

    /// <summary>The code a correction stores for the food with this id.</summary>
    /// <param name="id">The food's id.</param>
    public static string CodeOf(Guid id) => Prefix + id.ToString("N", CultureInfo.InvariantCulture);

    /// <summary>Whether a code names a household's food rather than a BLS one.</summary>
    /// <param name="code">A food code.</param>
    public static bool IsOwn(string code)
    {
        ArgumentNullException.ThrowIfNull(code);

        return code.StartsWith(Prefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Reads a label: a name, and per 100 g the kilocalories and what else it gives. Kilojoules follow from
    /// kilocalories when left out; anything else left out is unknown.
    /// </summary>
    /// <param name="id">The food's id.</param>
    /// <param name="name">What the household calls it: "Kokosmilch light".</param>
    /// <param name="per100Grams">The label's values per 100 g.</param>
    public static Result<Food> Create(Guid id, string? name, Nutrients per100Grams)
    {
        ArgumentNullException.ThrowIfNull(per100Grams);

        var trimmed = name?.Trim() ?? string.Empty;
        var values = per100Grams with
        {
            EnergyKj = per100Grams.EnergyKj ??
                       (per100Grams.EnergyKcal is { } kcal ? Math.Round(kcal * KilojoulesPerKilocalorie) : null)
        };

        return trimmed.Length is > 0 and <= LongestName && IsPlausible(values)
            ? new Food(CodeOf(id), trimmed, trimmed, values)
            : NutritionErrors.InvalidFood;
    }

    /// <summary>
    /// Where a food's values came from: Open Food Facts when they are the ones it gave for the barcode, edited when the
    /// person changed any of them on the way (kilojoules aside, which follow from kilocalories) or what it gave is no
    /// longer known, else the label.
    /// </summary>
    /// <param name="per100Grams">The values the person saved.</param>
    /// <param name="fromCatalogue">Whether the person took them over from Open Food Facts.</param>
    /// <param name="offered">What Open Food Facts gave for the barcode, as far as the server still knows.</param>
    public static string SourceOf(Nutrients per100Grams, bool fromCatalogue, Nutrients? offered)
    {
        ArgumentNullException.ThrowIfNull(per100Grams);

        if (!fromCatalogue)
        {
            return HouseholdFoodSources.Package;
        }

        return offered is not null && per100Grams with { EnergyKj = null } == offered with { EnergyKj = null }
            ? HouseholdFoodSources.OpenFoodFacts
            : HouseholdFoodSources.OpenFoodFactsEdited;
    }

    // Nothing negative, no more of a part than of the whole, and no more than 100 g can hold.
    private static bool IsPlausible(Nutrients values) =>
        values.EnergyKcal is > 0 and <= MostKcal &&
        values.EnergyKj is > 0 and <= MostKcal * KilojoulesPerKilocalorie + 1 &&
        new[] { values.Fat, values.SaturatedFat, values.Carbohydrate, values.Sugars, values.Protein, values.Salt }
            .All(grams => grams is null or (>= 0 and <= MostGrams)) &&
        !(values.SaturatedFat > values.Fat) &&
        !(values.Sugars > values.Carbohydrate);
}

/// <summary>Where a household food's values come from, as stored and as the client reads them.</summary>
public static class HouseholdFoodSources
{
    /// <summary>Typed from the package's label.</summary>
    public const string Package = "package";

    /// <summary>Taken over from Open Food Facts as it gave them.</summary>
    public const string OpenFoodFacts = "openFoodFacts";

    /// <summary>Taken over from Open Food Facts and corrected by the person.</summary>
    public const string OpenFoodFactsEdited = "openFoodFactsEdited";
}
