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

    /// <summary>The longest a recipe's own word for its yield may be.</summary>
    /// <remarks>
    /// Room for "Gläser à 400 ml" and not for a sentence. It is a noun that
    /// sits beside a number in a stepper, and a label that wraps to three lines
    /// there is a label nobody can read at a glance.
    /// </remarks>
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

    /// <summary>
    /// The recipe's own word for what it makes — "Cake", "Gläser", "Blech".
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null for nearly every recipe, and that is the ordinary case: the word is
    /// then derived from <see cref="Kind"/> in whichever language the reader
    /// has chosen, which is the right answer for anything measured in portions
    /// or in countable things.
    /// </para>
    /// <para>
    /// It replaces the <em>wording</em> and nothing else. <see cref="Kind"/>
    /// still decides how the servings control counts and how far a recipe may
    /// be scaled, because "this makes one cake" and "one more, please" are
    /// different questions and only the first has an answer here.
    /// </para>
    /// <para>
    /// Written as typed, never pluralised. A label is one word in one person's
    /// language and guessing its plural is how "2 Gläsers" happens.
    /// </para>
    /// </remarks>
    public string? Label { get; }

    /// <summary>What a recipe makes until someone says otherwise.</summary>
    public static Yield Default { get; } = new(4m, YieldKind.Servings, label: null);

    /// <summary>Creates a yield.</summary>
    /// <param name="amount">How many.</param>
    /// <param name="kind">Of what.</param>
    /// <param name="label">The recipe's own word for it, or null for the usual one.</param>
    public static Result<Yield> Create(decimal amount, YieldKind kind, string? label = null)
    {
        if (amount is <= 0 or > MaxAmount)
        {
            return RecipeErrors.InvalidYield;
        }

        // Blank is not a word, so it is the absence of one. Otherwise an author
        // who clears the field would be left with a recipe that makes "4 ".
        var worded = string.IsNullOrWhiteSpace(label) ? null : label.Trim();

        return worded?.Length > MaxLabelLength
            ? RecipeErrors.InvalidYieldLabel
            : new Yield(amount, kind, worded);
    }
}
