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

    /// <summary>The unit is not one that stands for a weight of this food.</summary>
    NotInGrams = 1,

    /// <summary>The recipe states no amount.</summary>
    NoAmount = 2
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
/// which are sold in legally defined sizes. Everything else is not counted. Any change here must
/// raise <see cref="NutritionData.Version"/>.
/// </remarks>
public static class NutritionGrams
{
    private const decimal MillilitresPerTeaspoon = 5m;
    private const decimal MillilitresPerTablespoon = 15m;

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
            UnitFamily.Volume => ByDensity(amount * Units.ToCanonicalFactor(quantity.Unit), food),
            UnitFamily.Spoon => ByDensity(amount * MillilitresPerSpoon(quantity.Unit!), food),
            _ => ByCount(quantity, amount, food)
        };
    }

    private static decimal MillilitresPerSpoon(Unit spoon) =>
        spoon == Unit.Teaspoon ? MillilitresPerTeaspoon : MillilitresPerTablespoon;

    private static GramsReading ByDensity(decimal millilitres, FoodName food) =>
        food.Density is { } density
            ? GramsReading.Counted(millilitres * density, GramsBasis.Density)
            : GramsReading.Refused(GramsRefusal.NotInGrams);

    private static GramsReading ByCount(Quantity quantity, decimal amount, FoodName food)
    {
        var eggGrams = food.Egg switch
        {
            EggPart.Whole => WholeEggGrams,
            EggPart.Yolk => YolkGrams,
            EggPart.White => WhiteGrams,
            _ => 0m
        };

        var isCount = quantity.Unit is null || quantity.Unit == Unit.Piece;

        return isCount && eggGrams > 0
            ? GramsReading.Counted(amount * eggGrams, GramsBasis.EggSize)
            : GramsReading.Refused(GramsRefusal.NotInGrams);
    }
}
