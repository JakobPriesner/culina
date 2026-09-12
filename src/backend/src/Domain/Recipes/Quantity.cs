using Domain.Shared;

namespace Domain.Recipes;

/// <summary>
/// How much of an ingredient a recipe calls for.
/// </summary>
/// <remarks>
/// Both parts are optional, because both absences are real: "salt" has no
/// amount and no unit, and "2 eggs" has an amount but no unit. Amounts are
/// <c>decimal</c> and never <c>double</c> — 0.1 litres is an ordinary thing for
/// a recipe to ask for and binary floating point cannot represent it.
/// </remarks>
public sealed record Quantity
{
    /// <summary>The largest amount a recipe may plausibly call for.</summary>
    public const decimal MaxAmount = 1_000_000m;

    private Quantity(decimal? amount, Unit? unit)
    {
        Amount = amount;
        Unit = unit;
    }

    /// <summary>How much, or null when the recipe does not say.</summary>
    public decimal? Amount { get; }

    /// <summary>In what, or null for a bare count or no measurement at all.</summary>
    public Unit? Unit { get; }

    /// <summary>An ingredient with no stated amount, like salt.</summary>
    public static Quantity Unmeasured { get; } = new(amount: null, unit: null);

    /// <summary>Creates a quantity.</summary>
    /// <param name="amount">How much, or null.</param>
    /// <param name="unit">In what, or null.</param>
    public static Result<Quantity> Create(decimal? amount, Unit? unit)
    {
        if (amount is null)
        {
            // A unit without an amount would render as "g of butter", so the
            // unit is dropped rather than kept as a half-measurement.
            return Unmeasured;
        }

        return amount is <= 0 or > MaxAmount
            ? RecipeErrors.InvalidQuantity
            : new Quantity(amount, unit);
    }

    /// <summary>Whether this amount is stated at all.</summary>
    public bool IsMeasured => Amount is not null;

    /// <summary>Whether scaling this amount is meaningful.</summary>
    public bool Scales => IsMeasured && Units.Scales(Unit);

    /// <summary>Whether this and <paramref name="other"/> can be added.</summary>
    /// <param name="other">The amount to add.</param>
    public bool CanCombineWith(Quantity other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return IsMeasured && other.IsMeasured && Units.CanCombine(Unit, other.Unit);
    }

    /// <summary>
    /// Adds another amount, in the family's canonical unit.
    /// </summary>
    /// <param name="other">The amount to add.</param>
    /// <remarks>
    /// The sum is deliberately left unrounded. Rounding on the way in and then
    /// summing compounds the error, and a shopping list that adds five recipes
    /// would drift visibly.
    /// </remarks>
    public Result<Quantity> Add(Quantity other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!CanCombineWith(other))
        {
            return RecipeErrors.IncompatibleUnits;
        }

        var canonical = Units.CanonicalOf(Unit);
        var total = (Amount!.Value * Units.ToCanonicalFactor(Unit))
            + (other.Amount!.Value * Units.ToCanonicalFactor(other.Unit));

        return Create(total, canonical);
    }

    /// <summary>This amount expressed in its family's canonical unit.</summary>
    public Quantity ToCanonical() =>
        IsMeasured
            ? new Quantity(Amount!.Value * Units.ToCanonicalFactor(Unit), Units.CanonicalOf(Unit))
            : this;
}
