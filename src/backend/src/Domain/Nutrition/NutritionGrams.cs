using Domain.Recipes;

namespace Domain.Nutrition;

/// <summary>How a recipe amount became grams.</summary>
public enum GramsBasis
{
    /// <summary>Not counted.</summary>
    None = 0,

    /// <summary>The amount was a mass already.</summary>
    Mass = 1,

    /// <summary>A volume or spoons, times the food's density.</summary>
    Density = 2,

    /// <summary>Eggs by count, at EU size class M.</summary>
    EggSize = 3,

    /// <summary>The household's own weight for this ingredient and unit: exact as far as the household is concerned.</summary>
    HouseholdWeight = 4,

    /// <summary>A typical weight of the food in that unit (<see cref="TypicalWeights"/>): an estimate.</summary>
    TypicalWeight = 5
}

/// <summary>Why an amount is not counted.</summary>
public enum GramsRefusal
{
    /// <summary>Counted.</summary>
    None = 0,

    /// <summary>The recipe states no amount.</summary>
    NoAmount = 1,

    /// <summary>A teaspoon or tablespoon of a food without a density: flour, sugar, butter, tomato paste.</summary>
    SpoonOfSolid = 2,

    /// <summary>A volume (millilitres, litres, a US cup, a fluid ounce) of a food without a density.</summary>
    VolumeOfSolid = 3,

    /// <summary>A bare count or a piece of a food that is not an egg: an onion.</summary>
    Count = 4,

    /// <summary>Any other unit: a clove, a bunch, a can, a pack, a pinch, a household's own word.</summary>
    HouseholdUnit = 5
}

/// <summary>Either the grams a line stands for and how they were reached, or why it counts nothing.</summary>
public sealed record GramsReading
{
    private GramsReading(decimal? grams, GramsBasis basis, GramsRefusal refusal, TypicalWeight? typical)
    {
        Grams = grams;
        Basis = basis;
        Refusal = refusal;
        Typical = typical;
    }

    /// <summary>The grams, or null when the line is not counted.</summary>
    public decimal? Grams { get; }

    /// <summary>How the grams were reached; <see cref="GramsBasis.None"/> when not counted.</summary>
    public GramsBasis Basis { get; }

    /// <summary>Why the line is not counted; <see cref="GramsRefusal.None"/> when it is.</summary>
    public GramsRefusal Refusal { get; }

    /// <summary>The typical weight the grams came from, for <see cref="GramsBasis.TypicalWeight"/>; null otherwise.</summary>
    public TypicalWeight? Typical { get; }

    internal static GramsReading Counted(decimal grams, GramsBasis basis, TypicalWeight? typical = null) =>
        new(grams, basis, GramsRefusal.None, typical);

    internal static GramsReading Refused(GramsRefusal refusal) => new(null, GramsBasis.None, refusal, null);
}

/// <summary>
/// What a recipe line weighs, for nutrition only.
/// </summary>
/// <remarks>
/// Not a general conversion, and not a way round <see cref="Units"/>, which never turns a spoon into
/// millilitres: that rule protects an amount shown to a cook and a shopping-list merge. Here the
/// grams are added into a figure whose own uncertainty is far larger than a spoon's size, and the
/// reading says how they were reached. A spoon or a volume counts only for a food that pours
/// (<see cref="FoodName.Density"/>); a spoon of flour counts only by a typical weight. A bare count counts
/// for eggs, which are sold in legally defined sizes, and otherwise only by a household's weight or a
/// typical weight. A US cup (236.588 ml) and a US fluid ounce (29.5735 ml)
/// count like millilitres for a food that pours, for the same reason as spoons: unlike a German
/// "Tasse", "Becher" or "Glas" they have a standard size. Everything else is not counted, and the
/// refusal says which rule refused it.
/// <para>
/// The order, for one line: no amount; mass; the household's weight for the ingredient in this unit
/// (never for a volume, which keeps the density rule); volume and spoons by density; eggs by count; a
/// typical weight, only when the household allows it; the refusals. Any change here must raise
/// <see cref="NutritionData.Version"/>.
/// </para>
/// </remarks>
public static class NutritionGrams
{
    private const decimal MillilitresPerTeaspoon = 5m;
    private const decimal MillilitresPerTablespoon = 15m;
    private const decimal MillilitresPerUsCup = 236.588m;
    private const decimal MillilitresPerUsFluidOunce = 29.5735m;

    /// <summary>
    /// The most a line in grams can be of a food's powder before it is read as the liquid. No recipe uses
    /// more than about 50 g of broth powder (that makes over 20 litres of broth), and nobody weighs less
    /// than 50 g of liquid broth, so the weight says which one the line means: "4 g Brühe" is the
    /// powder, "500 g Brühe" is the liquid, counted by mass.
    /// </summary>
    internal const decimal MostGramsOfPowder = 50m;

    /// <summary>The most one unit may be said to weigh: no onion, can or pack weighs 10 kg.</summary>
    public const decimal MostGramsPerUnit = 10_000m;

    /// <summary>
    /// The edible weight of one EU size class M egg: Regulation (EC) 589/2008 gives M as 53 to 63 g
    /// in the shell (midpoint 58 g); USDA FoodData Central SR Legacy, "Egg, whole, raw, fresh"
    /// (fdc 171287) has a large egg as 50 g edible of about 57 g in the shell, so 12 % is shell and
    /// 58 g leaves about 51 g.
    /// </summary>
    internal const decimal WholeEggGrams = 51m;

    /// <summary>
    /// The yolk of that egg, in the proportion of the USDA large egg: 17 g of its 50 g edible.
    /// </summary>
    internal const decimal YolkGrams = WholeEggGrams * 17m / 50m;

    /// <summary>The white of that egg, by the same proportion: 33 g of 50 g.</summary>
    internal const decimal WhiteGrams = WholeEggGrams * 33m / 50m;

    /// <summary>
    /// The food a line is, when the entry has a liquid and a powder (broth): the liquid for a volume (ml,
    /// l, US cup, fluid ounce) and for more than <see cref="MostGramsOfPowder"/> grams, the powder for
    /// fewer grams, a spoon (which packs, so it is not counted), a count or a household unit. An entry
    /// without a liquid is its own answer.
    /// </summary>
    /// <param name="quantity">How much the recipe calls for.</param>
    /// <param name="food">The food it was recognised as.</param>
    public static FoodName Resolve(Quantity quantity, FoodName food)
    {
        ArgumentNullException.ThrowIfNull(quantity);
        ArgumentNullException.ThrowIfNull(food);

        var asLiquid = Units.FamilyOf(quantity.Unit) switch
        {
            UnitFamily.Volume => true,
            UnitFamily.Mass => quantity.Amount * Units.ToCanonicalFactor(quantity.Unit) > MostGramsOfPowder,
            UnitFamily.Spoon => false,
            _ => MillilitresOfUsMeasure(quantity.Unit) is not null
        };

        return food.Resolved(asLiquid);
    }

    /// <summary>
    /// The unit key a line's amount is weighed in (see <see cref="UnitKeys"/>), which is the key a household's
    /// weight or a typical weight is stored under; null when the line has no amount, a mass, or a volume,
    /// which have a size of their own. A bare count or a piece of a name that says its unit ("Knoblauchzehen")
    /// is that unit.
    /// </summary>
    /// <param name="quantity">How much the recipe calls for.</param>
    /// <param name="food">The food it was recognised as.</param>
    public static string? WeighedUnitKey(Quantity quantity, FoodName food)
    {
        ArgumentNullException.ThrowIfNull(quantity);
        ArgumentNullException.ThrowIfNull(food);

        if (quantity.Amount is null || HasOwnSize(quantity.Unit))
        {
            return null;
        }

        var key = UnitKeys.Of(quantity.Unit);

        return key == UnitKeys.Piece && food.ImpliedUnit is { } implied ? implied : key;
    }

    /// <summary>Whether a unit is a mass or a volume, which has a size of its own that no household weight overrides.</summary>
    /// <param name="unit">The unit, or null for none.</param>
    public static bool HasOwnSize(Unit? unit) =>
        Units.FamilyOf(unit) is UnitFamily.Mass or UnitFamily.Volume || MillilitresOfUsMeasure(unit) is not null;

    /// <summary>Reads the grams a recipe line stands for, or says why not.</summary>
    /// <param name="quantity">How much the recipe calls for.</param>
    /// <param name="food">The food it was recognised as.</param>
    /// <param name="householdWeights">What the household says one unit of this ingredient weighs, by unit key; null for none.</param>
    /// <param name="useTypicalWeights">Whether a typical weight may count a line nothing else counts.</param>
    public static GramsReading Read(
        Quantity quantity,
        FoodName food,
        IReadOnlyDictionary<string, decimal>? householdWeights = null,
        bool useTypicalWeights = false)
    {
        ArgumentNullException.ThrowIfNull(quantity);
        ArgumentNullException.ThrowIfNull(food);

        if (quantity.Amount is not { } amount)
        {
            return GramsReading.Refused(GramsRefusal.NoAmount);
        }

        return Units.FamilyOf(quantity.Unit) switch
        {
            UnitFamily.Mass => GramsReading.Counted(amount * Units.ToCanonicalFactor(quantity.Unit), GramsBasis.Mass),
            UnitFamily.Volume => ByDensity(
                amount * Units.ToCanonicalFactor(quantity.Unit), food, GramsRefusal.VolumeOfSolid),
            _ when MillilitresOfUsMeasure(quantity.Unit) is { } millilitres => ByDensity(
                amount * millilitres, food, GramsRefusal.VolumeOfSolid),
            var family => ByItem(quantity, amount, food, family == UnitFamily.Spoon, householdWeights, useTypicalWeights)
        };
    }

    // The spellings that reach here as household units: see UnitSpellings, which has no entry for them.
    private static decimal? MillilitresOfUsMeasure(Unit? unit) =>
        unit?.Code.ToLowerInvariant() switch
        {
            "cup" or "cups" => MillilitresPerUsCup,
            "fl oz" or "fl. oz" => MillilitresPerUsFluidOunce,
            _ => null
        };

    private static decimal MillilitresPerSpoon(Unit spoon) =>
        spoon == Unit.Teaspoon ? MillilitresPerTeaspoon : MillilitresPerTablespoon;

    private static GramsReading ByDensity(decimal millilitres, FoodName food, GramsRefusal withoutDensity) =>
        food.Density is { } density
            ? GramsReading.Counted(millilitres * density, GramsBasis.Density)
            : GramsReading.Refused(withoutDensity);

    // Spoons and every count unit: the household's weight, then density (spoons), then eggs (a count), then a typical weight.
    private static GramsReading ByItem(
        Quantity quantity,
        decimal amount,
        FoodName food,
        bool spoon,
        IReadOnlyDictionary<string, decimal>? householdWeights,
        bool useTypicalWeights)
    {
        var unitKey = WeighedUnitKey(quantity, food)!;

        if (householdWeights is not null && householdWeights.TryGetValue(unitKey, out var own))
        {
            return GramsReading.Counted(amount * own, GramsBasis.HouseholdWeight);
        }

        if (spoon && food.Density is { } density)
        {
            return GramsReading.Counted(amount * MillilitresPerSpoon(quantity.Unit!) * density, GramsBasis.Density);
        }

        var eggGrams = unitKey != UnitKeys.Piece ? 0m : food.Egg switch
        {
            EggPart.Whole => WholeEggGrams,
            EggPart.Yolk => YolkGrams,
            EggPart.White => WhiteGrams,
            _ => 0m
        };

        if (eggGrams > 0)
        {
            return GramsReading.Counted(amount * eggGrams, GramsBasis.EggSize);
        }

        if (useTypicalWeights && TypicalWeights.Find(food.Code, unitKey) is { } typical)
        {
            return GramsReading.Counted(amount * typical.Grams, GramsBasis.TypicalWeight, typical);
        }

        return GramsReading.Refused(spoon ? GramsRefusal.SpoonOfSolid
            : unitKey == UnitKeys.Piece ? GramsRefusal.Count
            : GramsRefusal.HouseholdUnit);
    }
}
