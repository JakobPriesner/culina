namespace Domain.Recipes;

/// <summary>What each unit is, and what it can be converted to.</summary>
/// <remarks>
/// Spoons are deliberately not convertible to millilitres (a tablespoon is 14.8, 15 or 20 ml
/// depending on region); do not "fix" this. Count units only add to the identical unit.
/// </remarks>
public static class Units
{
    /// <summary>The family a unit belongs to.</summary>
    /// <param name="unit">The unit, or null for no unit at all.</param>
    /// <remarks>An unknown unit counts things, so a household's own unit scales and sums only with itself.</remarks>
    public static UnitFamily FamilyOf(Unit? unit) => Lower(unit) switch
    {
        null => UnitFamily.None,
        "g" or "kg" => UnitFamily.Mass,
        "ml" or "l" => UnitFamily.Volume,
        "tsp" or "tbsp" => UnitFamily.Spoon,
        _ => UnitFamily.Count
    };

    /// <summary>The unit a family is summed in.</summary>
    /// <param name="unit">Any unit of the family.</param>
    public static Unit? CanonicalOf(Unit? unit) => FamilyOf(unit) switch
    {
        UnitFamily.Mass => Unit.Gram,
        UnitFamily.Volume => Unit.Millilitre,
        _ => unit
    };

    /// <summary>How many canonical units one of this unit is worth.</summary>
    /// <param name="unit">The unit to convert from.</param>
    public static decimal ToCanonicalFactor(Unit? unit) => Lower(unit) switch
    {
        "kg" or "l" => 1000m,
        _ => 1m
    };

    /// <summary>Whether two amounts in these units can be added together.</summary>
    /// <param name="left">One unit.</param>
    /// <param name="right">The other unit.</param>
    public static bool CanCombine(Unit? left, Unit? right)
    {
        var family = FamilyOf(left);

        if (family != FamilyOf(right))
        {
            return false;
        }

        return family switch
        {
            UnitFamily.Mass or UnitFamily.Volume => true,
            _ => left == right
        };
    }

    /// <summary>Whether scaling this unit's amount is meaningful.</summary>
    /// <param name="unit">The unit to check.</param>
    /// <remarks>A pinch is a gesture, not a measurement, so it does not scale.</remarks>
    public static bool Scales(Unit? unit) => Lower(unit) != "pinch";

    private static string? Lower(Unit? unit) => unit?.Code.ToLowerInvariant();
}
