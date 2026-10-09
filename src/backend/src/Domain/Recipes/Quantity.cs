using Domain.Shared;

namespace Domain.Recipes;

/// <summary>
/// How much of an ingredient a recipe calls for; amount and unit are each optional, and amounts are
/// <c>decimal</c>, never <c>double</c>.
/// </summary>
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
    public static Result<Quantity> Create(decimal? amount, Unit? unit)
    {
        if (amount is null)
        {
            // A unit without an amount would render as "g of butter", so it is dropped.
            return Unmeasured;
        }

        var rounded = Amounts.Round(amount.Value);

        return rounded is <= 0 or > MaxAmount
            ? RecipeErrors.InvalidQuantity
            : new Quantity(rounded, unit);
    }

    /// <summary>Whether this amount is stated at all.</summary>
    public bool IsMeasured => Amount is not null;

    /// <summary>Whether scaling this amount is meaningful.</summary>
    public bool Scales => IsMeasured && Units.Scales(Unit);

    /// <summary>Whether this and <paramref name="other"/> can be added.</summary>
    public bool CanCombineWith(Quantity other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return IsMeasured && other.IsMeasured && Units.CanCombine(Unit, other.Unit);
    }

    /// <summary>
    /// Adds another amount, in the family's canonical unit.
    /// </summary>
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

    /// <summary>
    /// What is left after taking <paramref name="other"/> away, in the canonical unit, or null when
    /// nothing is.
    /// </summary>
    /// <remarks>
    /// An amount that cannot be combined was never part of this one, so this is returned unchanged.
    /// </remarks>
    public Quantity? Without(Quantity other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!CanCombineWith(other))
        {
            return this;
        }

        var left = (Amount!.Value * Units.ToCanonicalFactor(Unit))
            - (other.Amount!.Value * Units.ToCanonicalFactor(other.Unit));

        return Create(left, Units.CanonicalOf(Unit)).Match<Quantity?>(quantity => quantity, _ => null);
    }

    /// <summary>This amount expressed in its family's canonical unit.</summary>
    public Quantity ToCanonical() =>
        IsMeasured
            ? new Quantity(Amount!.Value * Units.ToCanonicalFactor(Unit), Units.CanonicalOf(Unit))
            : this;
}
