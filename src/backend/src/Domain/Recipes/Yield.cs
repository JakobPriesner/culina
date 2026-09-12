using Domain.Shared;

namespace Domain.Recipes;

/// <summary>What a recipe makes.</summary>
public enum YieldKind
{
    /// <summary>Portions for people.</summary>
    Servings = 0,

    /// <summary>Countable things: muffins, biscuits, rolls.</summary>
    Pieces = 1
}

/// <summary>
/// How much a recipe makes.
/// </summary>
/// <remarks>
/// A kind as well as a number, so "12 muffins" scales as honestly as "4
/// portions" — and so the servings control can step by six for muffins and by
/// one for portions, which is what a person would actually do.
/// </remarks>
public sealed record Yield
{
    /// <summary>The most a single recipe may claim to make.</summary>
    public const decimal MaxAmount = 1000m;

    private Yield(decimal amount, YieldKind kind)
    {
        Amount = amount;
        Kind = kind;
    }

    /// <summary>How many.</summary>
    public decimal Amount { get; }

    /// <summary>Of what.</summary>
    public YieldKind Kind { get; }

    /// <summary>What a recipe makes until someone says otherwise.</summary>
    public static Yield Default { get; } = new(4m, YieldKind.Servings);

    /// <summary>Creates a yield.</summary>
    /// <param name="amount">How many.</param>
    /// <param name="kind">Of what.</param>
    public static Result<Yield> Create(decimal amount, YieldKind kind) =>
        amount is <= 0 or > MaxAmount
            ? RecipeErrors.InvalidYield
            : new Yield(amount, kind);
}
