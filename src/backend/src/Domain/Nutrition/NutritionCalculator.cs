using Domain.Recipes;
using Domain.Shopping;

namespace Domain.Nutrition;

/// <summary>What became of one ingredient line.</summary>
public enum LineStatus
{
    /// <summary>Its grams are in the totals.</summary>
    Counted = 0,

    /// <summary>The food is known, but the unit is not a weight of it.</summary>
    AmountNotInGrams = 1,

    /// <summary>The recipe states no amount.</summary>
    NoAmount = 2,

    /// <summary>The name is not one of ours.</summary>
    UnknownFood = 3,

    /// <summary>The household said not to count it.</summary>
    Excluded = 4
}

/// <summary>One ingredient line's part in a nutrition figure.</summary>
/// <param name="IngredientId">The recipe line.</param>
/// <param name="Status">What became of it.</param>
/// <param name="Food">The food it was counted as; null when it is not counted or has none.</param>
/// <param name="Grams">The grams counted; null when not counted.</param>
/// <param name="Via">How the grams were reached; <see cref="GramsBasis.None"/> when not counted.</param>
/// <param name="Corrected">Whether the household chose the food, not the name table.</param>
/// <param name="EnergyKcal">Its share of the energy per portion, unrounded; null when not counted or the food has no value.</param>
public sealed record NutritionLine(
    Guid IngredientId,
    LineStatus Status,
    Food? Food,
    decimal? Grams,
    GramsBasis Via,
    bool Corrected,
    decimal? EnergyKcal);

/// <summary>A value of the label, and whether it is only a lower bound.</summary>
/// <param name="Value">What was summed, unrounded.</param>
/// <param name="AtLeast">True when something left out could only have added to it.</param>
public readonly record struct LabelValue(decimal Value, bool AtLeast);

/// <summary>The label values of one portion or piece.</summary>
public sealed record LabelValues(
    LabelValue EnergyKj,
    LabelValue EnergyKcal,
    LabelValue Fat,
    LabelValue SaturatedFat,
    LabelValue Carbohydrate,
    LabelValue Sugars,
    LabelValue Protein,
    LabelValue Salt);

/// <summary>A recipe's nutrition: per portion or per piece, and which lines it covers.</summary>
/// <param name="Per">Whether the figures are per serving or per piece.</param>
/// <param name="Yield">The amount they were divided by.</param>
/// <param name="Values">The label values.</param>
/// <param name="Ingredients">Every line, in recipe order.</param>
public sealed record NutritionResult(
    YieldKind Per,
    decimal Yield,
    LabelValues Values,
    IReadOnlyList<NutritionLine> Ingredients)
{
    /// <summary>How many lines are counted.</summary>
    public int Counted => Ingredients.Count(line => line.Status == LineStatus.Counted);

    /// <summary>How many lines the recipe has.</summary>
    public int Lines => Ingredients.Count;

    /// <summary>Whether every line is counted. A value can still be a lower bound when a food lacks it.</summary>
    public bool Complete => Lines > 0 && Counted == Lines;
}

/// <summary>
/// Turns a recipe's ingredient lines into per-portion nutrition. Pure: the food table and the
/// household's corrections come in as arguments.
/// </summary>
/// <remarks>
/// Every value of every food is non-negative, so a sum over the lines that could be counted is a
/// lower bound on the whole; a value is flagged <see cref="LabelValue.AtLeast"/> when a line was
/// left out or a counted food lacks it. Nothing is rounded here. Per portion does not change when a
/// recipe is scaled (amounts and yield move together), so no factor is applied.
/// </remarks>
public static class NutritionCalculator
{
    /// <summary>Calculates the nutrition of a recipe.</summary>
    /// <param name="ingredients">Every ingredient line, all groups, in order.</param>
    /// <param name="yield">What the recipe makes.</param>
    /// <param name="findFood">The food with a BLS code, or null.</param>
    /// <param name="corrections">
    /// This household's choices, keyed by <see cref="ItemName.Fold"/> of the name: a BLS code, or
    /// null for "do not count".
    /// </param>
    public static NutritionResult Calculate(
        IEnumerable<RecipeIngredient> ingredients,
        Yield yield,
        Func<string, Food?> findFood,
        IReadOnlyDictionary<string, string?> corrections)
    {
        ArgumentNullException.ThrowIfNull(ingredients);
        ArgumentNullException.ThrowIfNull(yield);
        ArgumentNullException.ThrowIfNull(findFood);
        ArgumentNullException.ThrowIfNull(corrections);

        var lines = ingredients.Select(line => Read(line, findFood, corrections, yield.Amount)).ToList();

        return new NutritionResult(yield.Kind, yield.Amount, Sum(lines, yield.Amount), lines);
    }

    private static NutritionLine Read(
        RecipeIngredient ingredient,
        Func<string, Food?> findFood,
        IReadOnlyDictionary<string, string?> corrections,
        decimal yield)
    {
        var uncounted = (LineStatus status, bool corrected) =>
            new NutritionLine(ingredient.Id, status, null, null, GramsBasis.None, corrected, null);

        FoodName? name;
        var corrected = corrections.TryGetValue(ItemName.Fold(ingredient.Name), out var code);

        if (corrected && code is null)
        {
            return uncounted(LineStatus.Excluded, true);
        }

        if (corrected)
        {
            // A food nobody wrote density or egg words for counts by mass only.
            name = FoodNames.All.FirstOrDefault(entry => entry.Code == code)
                ?? new FoodName(code!, [], [], Density: null, EggPart.None);
        }
        else
        {
            name = FoodNames.Match(ingredient.Name);
        }

        var food = name is null ? null : findFood(name.Code);

        if (name is null || food is null)
        {
            return uncounted(LineStatus.UnknownFood, corrected);
        }

        var reading = NutritionGrams.Read(ingredient.Quantity, name);

        if (reading.Grams is not { } grams)
        {
            var status = reading.Refusal == GramsRefusal.NoAmount ? LineStatus.NoAmount : LineStatus.AmountNotInGrams;

            return new NutritionLine(ingredient.Id, status, food, null, GramsBasis.None, corrected, null);
        }

        return new NutritionLine(
            ingredient.Id,
            LineStatus.Counted,
            food,
            grams,
            reading.Basis,
            corrected,
            Share(grams, food.Per100Grams.EnergyKcal, yield));
    }

    private static LabelValues Sum(List<NutritionLine> lines, decimal yield)
    {
        var counted = lines.Where(line => line.Status == LineStatus.Counted).ToList();
        var leftOut = counted.Count == 0 || counted.Count < lines.Count;

        LabelValue Of(Func<Nutrients, decimal?> value)
        {
            var total = 0m;
            var missing = leftOut;

            foreach (var line in counted)
            {
                if (Share(line.Grams!.Value, value(line.Food!.Per100Grams), yield) is { } share)
                {
                    total += share;
                }
                else
                {
                    missing = true;
                }
            }

            return new LabelValue(total, missing);
        }

        return new LabelValues(
            Of(n => n.EnergyKj),
            Of(n => n.EnergyKcal),
            Of(n => n.Fat),
            Of(n => n.SaturatedFat),
            Of(n => n.Carbohydrate),
            Of(n => n.Sugars),
            Of(n => n.Protein),
            Of(n => n.Salt));
    }

    private static decimal? Share(decimal grams, decimal? per100Grams, decimal yield) =>
        per100Grams is { } value ? grams * value / 100m / yield : null;
}
