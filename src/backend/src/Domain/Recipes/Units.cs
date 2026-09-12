namespace Domain.Recipes;

/// <summary>
/// What each unit is, and what it can be converted to.
/// </summary>
/// <remarks>
/// <para>
/// Spoons are deliberately not convertible to millilitres. A US tablespoon is
/// 14.8 ml, a metric one is 15 ml, an Australian one is 20 ml, and a recipe
/// rarely says which it meant. Converting would invent precision the recipe
/// never had, so Culina keeps spoons as spoons. Do not "fix" this.
/// </para>
/// <para>
/// Count units only ever add to the identical unit: three cloves and two
/// bunches is not five of anything.
/// </para>
/// </remarks>
public static class Units
{
    /// <summary>The family a unit belongs to.</summary>
    /// <param name="unit">The unit, or null for no unit at all.</param>
    public static UnitFamily FamilyOf(Unit? unit) => unit switch
    {
        null => UnitFamily.None,
        Unit.Gram or Unit.Kilogram => UnitFamily.Mass,
        Unit.Millilitre or Unit.Litre => UnitFamily.Volume,
        Unit.Teaspoon or Unit.Tablespoon => UnitFamily.Spoon,
        _ => UnitFamily.Count
    };

    /// <summary>
    /// The unit a family is summed in, so two amounts can be added without
    /// either losing precision.
    /// </summary>
    /// <param name="unit">Any unit of the family.</param>
    public static Unit? CanonicalOf(Unit? unit) => FamilyOf(unit) switch
    {
        UnitFamily.Mass => Unit.Gram,
        UnitFamily.Volume => Unit.Millilitre,
        // A spoon and a count are already canonical: there is nothing smaller
        // to express them in.
        _ => unit
    };

    /// <summary>
    /// How many canonical units one of this unit is worth.
    /// </summary>
    /// <param name="unit">The unit to convert from.</param>
    public static decimal ToCanonicalFactor(Unit? unit) => unit switch
    {
        Unit.Kilogram => 1000m,
        Unit.Litre => 1000m,
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
            // Mass and volume convert freely within themselves.
            UnitFamily.Mass or UnitFamily.Volume => true,
            // Everything else has to match exactly.
            _ => left == right
        };
    }

    /// <summary>Whether scaling this unit's amount is meaningful.</summary>
    /// <param name="unit">The unit to check.</param>
    /// <remarks>
    /// A pinch is a gesture, not a measurement. Doubling a recipe does not
    /// double the pinch of salt, and pretending otherwise is the kind of small
    /// lie that makes people stop trusting the scaling.
    /// </remarks>
    public static bool Scales(Unit? unit) => unit != Unit.Pinch;
}
