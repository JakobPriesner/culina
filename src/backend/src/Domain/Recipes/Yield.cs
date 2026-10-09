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

/// <summary>How much a recipe makes: a kind as well as a number, so muffins scale in sixes and portions in ones.</summary>
public sealed record Yield
{
    /// <summary>The most a single recipe may claim to make.</summary>
    public const decimal MaxAmount = 1000m;

    /// <summary>The longest a recipe's own word for its yield may be: room for "Gläser à 400 ml", not a sentence.</summary>
    public const int MaxLabelLength = 40;

    private Yield(decimal amount, YieldKind kind, string? label)
    {
        Amount = amount;
        Kind = kind;
        Label = label;
    }

    /// <summary>How many.</summary>
    public decimal Amount { get; }

    /// <summary>Of what.</summary>
    public YieldKind Kind { get; }

    /// <summary>The recipe's own word for what it makes — "Cake", "Gläser", "Blech" — or null for the usual derived wording.</summary>
    /// <remarks>
    /// Replaces the wording only: <see cref="Kind"/> still decides how scaling counts. Written as typed, never pluralised.
    /// </remarks>
    public string? Label { get; }

    /// <summary>What a recipe makes until someone says otherwise.</summary>
    public static Yield Default { get; } = new(4m, YieldKind.Servings, label: null);

    /// <summary>Creates a yield.</summary>
    public static Result<Yield> Create(decimal amount, YieldKind kind, string? label = null)
    {
        amount = Amounts.Round(amount);

        if (amount is <= 0 or > MaxAmount)
        {
            return RecipeErrors.InvalidYield;
        }

        // Blank is not a word: clearing the field must not leave a recipe that makes "4 ".
        var worded = string.IsNullOrWhiteSpace(label) ? null : label.Trim();

        return worded?.Length > MaxLabelLength
            ? RecipeErrors.InvalidYieldLabel
            : new Yield(amount, kind, worded);
    }
}
