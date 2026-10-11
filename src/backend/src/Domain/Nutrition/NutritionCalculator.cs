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
    Excluded = 4,

    /// <summary>The grams would be more than one portion plausibly holds, so it looks like a typo or a unit slip.</summary>
    Implausible = 5
}

/// <summary>One ingredient line's part in a nutrition figure.</summary>
/// <param name="IngredientId">The recipe line.</param>
/// <param name="Status">What became of it.</param>
/// <param name="Food">The food it was counted as; null when it is not counted or has none.</param>
/// <param name="Grams">The grams counted, or for an implausible line the grams it would have been; null otherwise.</param>
/// <param name="Via">How the grams were reached; <see cref="GramsBasis.None"/> when there are none.</param>
/// <param name="Corrected">Whether the household chose the food, not the name table.</param>
/// <param name="EnergyKcal">Its share of the energy per portion, unrounded; null when not counted or the food has no value.</param>
/// <param name="Refusal">Why the unit is not counted, for <see cref="LineStatus.AmountNotInGrams"/>; otherwise <see cref="GramsRefusal.None"/>.</param>
/// <param name="LabelDe">What a reader calls the food in German, or the BLS name of a food nobody wrote a label for; null with no food.</param>
/// <param name="LabelEn">The same in English.</param>
/// <param name="UnitKey">The canonical unit a household's weight for this line is stored under (see <see cref="UnitKeys"/>); null when the line has no amount, a mass or a volume.</param>
/// <param name="Source">Where the typical weight comes from, for <see cref="GramsBasis.TypicalWeight"/>; null otherwise.</param>
/// <param name="Variants">The alternatives to the food, itself included; null when it has none or the line has no food.</param>
public sealed record NutritionLine(
    Guid IngredientId,
    LineStatus Status,
    Food? Food,
    decimal? Grams,
    GramsBasis Via,
    bool Corrected,
    decimal? EnergyKcal,
    GramsRefusal Refusal = GramsRefusal.None,
    string? LabelDe = null,
    string? LabelEn = null,
    string? UnitKey = null,
    string? Source = null,
    IReadOnlyList<FoodVariantEnergy>? Variants = null)
{
    /// <summary>
    /// Whether this line, left out of the figure, could still add energy to it: it is not counted,
    /// the household did not choose to leave it out, and its food is unknown or has energy (or no
    /// energy value). An implausible amount counts, since the right one is unknown. See <see cref="CanRaise"/>.
    /// </summary>
    public bool CanRaiseEnergy => CanRaise(nutrients => nutrients.EnergyKcal);

    /// <summary>
    /// Whether this line, left out of the figure, could still add to a value: it is not counted, not
    /// excluded by the household, and its food is unknown, lacks the value or has more than zero of
    /// it. Food known to have exactly zero adds exactly zero, so it makes nothing a lower bound.
    /// </summary>
    internal bool CanRaise(Func<Nutrients, decimal?> value) => Status switch
    {
        LineStatus.Counted or LineStatus.Excluded => false,
        LineStatus.Implausible => true,
        _ => Food is null || value(Food.Per100Grams) is not { } amount || amount > 0m
    };
}

/// <summary>One alternative to a line's food, with its energy for comparing.</summary>
/// <param name="Code">The BLS code.</param>
/// <param name="LabelDe">What a reader calls it in German.</param>
/// <param name="LabelEn">What a reader calls it in English.</param>
/// <param name="EnergyKcal">Kilocalories per 100 g, from the table; null when it has none.</param>
public sealed record FoodVariantEnergy(string Code, string LabelDe, string LabelEn, decimal? EnergyKcal);

/// <summary>A value of the label, and whether it is only a lower bound.</summary>
/// <param name="Value">What was summed, unrounded.</param>
/// <param name="AtLeast">True when something left out could only have added to it.</param>
/// <param name="Estimated">True when a line that adds to it was counted by a typical weight, not by an amount or the household's own weight.</param>
public readonly record struct LabelValue(decimal Value, bool AtLeast, bool Estimated = false);

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
/// lower bound on the whole; a value is flagged <see cref="LabelValue.AtLeast"/> when a line that
/// could raise it was left out or a counted food lacks it. A left-out line whose food is known to
/// have exactly 0 of a value (salt and water have no energy) adds exactly 0 and flags nothing. A
/// line the household excluded is its choice: "do not count this", so it flags nothing either, but
/// a recipe with nothing counted and nothing but exclusions is still flagged, as the figure says
/// nothing of the dish. An unknown food or an implausible amount flags every value. Nothing is rounded here. Per portion does not change when a
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
    /// <param name="weights">
    /// What this household says one unit of an ingredient weighs: grams by unit key (<see cref="UnitKeys"/>),
    /// keyed by <see cref="ItemName.Fold"/> of the name.
    /// </param>
    /// <param name="useTypicalWeights">Whether <see cref="TypicalWeights"/> may count a line nothing else counts.</param>
    public static NutritionResult Calculate(
        IEnumerable<RecipeIngredient> ingredients,
        Yield yield,
        Func<string, Food?> findFood,
        IReadOnlyDictionary<string, string?> corrections,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>> weights,
        bool useTypicalWeights)
    {
        ArgumentNullException.ThrowIfNull(ingredients);
        ArgumentNullException.ThrowIfNull(yield);
        ArgumentNullException.ThrowIfNull(findFood);
        ArgumentNullException.ThrowIfNull(corrections);
        ArgumentNullException.ThrowIfNull(weights);

        var lines = ingredients
            .Select(line => Read(line, findFood, corrections, weights, useTypicalWeights, yield.Amount))
            .ToList();

        return new NutritionResult(yield.Kind, yield.Amount, Sum(lines, yield.Amount), lines);
    }

    /// <summary>
    /// The most one portion or piece of the recipe plausibly holds of a single ingredient, in grams.
    /// Nobody eats more than 2 kg of one thing in a portion, so a line above it is a slip: "1800 l
    /// milk" meant 1800 ml, or kg written for g. Left out of the sums, it makes them lower bounds.
    /// </summary>
    internal const decimal MaxGramsPerPortion = 2000m;

    /// <summary>
    /// A line must also weigh more than this in all before it is called a slip: many recipes say they
    /// make one portion when they make a whole cake, and 2.5 kg of flour there is the yield's mistake,
    /// not the amount's. 10 kg of one thing is beyond any home kitchen.
    /// </summary>
    internal const decimal MaxGramsPerLine = 10_000m;

    private static NutritionLine Read(
        RecipeIngredient ingredient,
        Func<string, Food?> findFood,
        IReadOnlyDictionary<string, string?> corrections,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, decimal>> weights,
        bool useTypicalWeights,
        decimal yield)
    {
        var uncounted = (LineStatus status, bool corrected) =>
            new NutritionLine(ingredient.Id, status, null, null, GramsBasis.None, corrected, null);

        var nameKey = ItemName.Fold(ingredient.Name);
        var corrected = corrections.TryGetValue(nameKey, out var code);

        if (corrected && code is null)
        {
            return uncounted(LineStatus.Excluded, true);
        }

        // The household chose that exact food, so its entry (for density and egg words) is not resolved
        // by unit; a name the table matched is, since broth is a powder or a liquid by its unit.
        var entry = corrected
            ? FoodNames.All.FirstOrDefault(one => one.Code == code)
            : FoodNames.Match(ingredient.Name);
        var name = entry is null || corrected ? entry : NutritionGrams.Resolve(ingredient.Quantity, entry);
        var food = corrected ? findFood(code!) : name is null ? null : findFood(name.Code);

        if (food is null)
        {
            return uncounted(LineStatus.UnknownFood, corrected);
        }

        // A food nobody wrote density, egg words or a label for counts by mass only and is named by BLS.
        name ??= Unnamed(food);

        var reading = NutritionGrams.Read(
            ingredient.Quantity, name, weights.GetValueOrDefault(nameKey), useTypicalWeights);
        var unitKey = NutritionGrams.WeighedUnitKey(ingredient.Quantity, name);

        // A can of "Tomaten" is canned tomatoes: the typical weight says which food the line is.
        if (reading.Typical?.CountAs is { } countedAs && findFood(countedAs) is { } counted)
        {
            food = counted;
            name = FoodNames.All.FirstOrDefault(one => one.Code == countedAs) ?? Unnamed(counted);
        }

        var known = (LineStatus status, decimal? grams, GramsBasis basis, decimal? energy, GramsRefusal refusal) =>
            new NutritionLine(
                ingredient.Id,
                status,
                food,
                grams,
                basis,
                corrected,
                energy,
                refusal,
                name.LabelDe,
                name.LabelEn,
                unitKey,
                reading.Typical?.Source,
                VariantsOf(food, findFood));

        if (reading.Grams is not { } grams)
        {
            return known(
                reading.Refusal == GramsRefusal.NoAmount ? LineStatus.NoAmount : LineStatus.AmountNotInGrams,
                null,
                GramsBasis.None,
                null,
                reading.Refusal == GramsRefusal.NoAmount ? GramsRefusal.None : reading.Refusal);
        }

        if (grams / yield > MaxGramsPerPortion && grams > MaxGramsPerLine)
        {
            return known(LineStatus.Implausible, grams, reading.Basis, null, GramsRefusal.None);
        }

        return known(
            LineStatus.Counted,
            grams,
            reading.Basis,
            Share(grams, food.Per100Grams.EnergyKcal, yield),
            GramsRefusal.None);
    }

    private static FoodName Unnamed(Food food) =>
        new(food.Code, [], [], Density: null, EggPart.None, food.NameDe, food.NameEn);

    private static List<FoodVariantEnergy>? VariantsOf(Food food, Func<string, Food?> findFood)
    {
        var group = FoodVariants.For(food.Code);

        return group.Count == 0
            ? null
            : [.. group.Select(one => new FoodVariantEnergy(
                one.Code, one.LabelDe, one.LabelEn, findFood(one.Code)?.Per100Grams.EnergyKcal))];
    }

    private static LabelValues Sum(List<NutritionLine> lines, decimal yield)
    {
        var counted = lines.Where(line => line.Status == LineStatus.Counted).ToList();
        var nothingToGoOn = lines.All(line => line.Status == LineStatus.Excluded);

        LabelValue Of(Func<Nutrients, decimal?> value)
        {
            var total = 0m;
            var missing = nothingToGoOn || lines.Any(line => line.CanRaise(value));
            var estimated = false;

            foreach (var line in counted)
            {
                if (Share(line.Grams!.Value, value(line.Food!.Per100Grams), yield) is { } share)
                {
                    total += share;
                    estimated |= share > 0m && line.Via == GramsBasis.TypicalWeight;
                }
                else
                {
                    missing = true;
                }
            }

            return new LabelValue(total, missing, estimated);
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
