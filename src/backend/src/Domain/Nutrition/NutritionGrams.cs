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
    EggSize = 3
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
    private GramsReading(decimal? grams, GramsBasis basis, GramsRefusal refusal)
    {
        Grams = grams;
        Basis = basis;
        Refusal = refusal;
    }

    /// <summary>The grams, or null when the line is not counted.</summary>
    public decimal? Grams { get; }

    /// <summary>How the grams were reached; <see cref="GramsBasis.None"/> when not counted.</summary>
    public GramsBasis Basis { get; }

    /// <summary>Why the line is not counted; <see cref="GramsRefusal.None"/> when it is.</summary>
    public GramsRefusal Refusal { get; }

    internal static GramsReading Counted(decimal grams, GramsBasis basis) => new(grams, basis, GramsRefusal.None);

    internal static GramsReading Refused(GramsRefusal refusal) => new(null, GramsBasis.None, refusal);
}

/// <summary>
/// What a recipe line weighs, for nutrition only.
/// </summary>
/// <remarks>
/// Not a general conversion, and not a way round <see cref="Units"/>, which never turns a spoon into
/// millilitres: that rule protects an amount shown to a cook and a shopping-list merge. Here the
/// grams are added into a figure whose own uncertainty is far larger than a spoon's size, and the
/// reading says how they were reached. A spoon or a volume counts only for a food that pours
/// (<see cref="FoodName.Density"/>); a spoon of flour never counts. A bare count counts only for eggs,
/// which are sold in legally defined sizes. A US cup (236.588 ml) and a US fluid ounce (29.5735 ml)
/// count like millilitres for a food that pours, for the same reason as spoons: unlike a German
/// "Tasse", "Becher" or "Glas" they have a standard size. Everything else is not counted, and the
/// refusal says which rule refused it. Any change here must
/// raise <see cref="NutritionData.Version"/>.
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

    /// <summary>Reads the grams a recipe line stands for, or says why not.</summary>
    /// <param name="quantity">How much the recipe calls for.</param>
    /// <param name="food">The food it was recognised as.</param>
    public static GramsReading Read(Quantity quantity, FoodName food)
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
            UnitFamily.Spoon => ByDensity(
                amount * MillilitresPerSpoon(quantity.Unit!), food, GramsRefusal.SpoonOfSolid),
            _ => ByCount(quantity, amount, food)
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

    private static GramsReading ByCount(Quantity quantity, decimal amount, FoodName food)
    {
        var eggGrams = food.Egg switch
        {
            EggPart.Whole => WholeEggGrams,
            EggPart.Yolk => YolkGrams,
            EggPart.White => WhiteGrams,
            _ => 0m
        };

        if (MillilitresOfUsMeasure(quantity.Unit) is { } millilitres)
        {
            return ByDensity(amount * millilitres, food, GramsRefusal.VolumeOfSolid);
        }

        if (quantity.Unit is not null && quantity.Unit != Unit.Piece)
        {
            return GramsReading.Refused(GramsRefusal.HouseholdUnit);
        }

        return eggGrams > 0
            ? GramsReading.Counted(amount * eggGrams, GramsBasis.EggSize)
            : GramsReading.Refused(GramsRefusal.Count);
    }
}
