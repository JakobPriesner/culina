using System.Globalization;
using Domain.Shared;

namespace Domain.Recipes;

/// <summary>What an ingredient amount is measured in.</summary>
/// <remarks>
/// A code, not an enum: the vocabulary is open, and a household adds a unit by writing it. Added units are
/// <see cref="UnitFamily.Count"/> (scale and sum, never convert). Compared case-insensitively, stored as written. See <see cref="Units"/>.
/// </remarks>
public sealed record Unit
{
    /// <summary>The longest unit the database column accepts.</summary>
    public const int MaxCodeLength = 16;

    private Unit(string code) => Code = code;

    /// <summary>How it is written, on the wire and in the database.</summary>
    public string Code { get; }

    /// <summary>Grams.</summary>
    public static Unit Gram { get; } = new("g");

    /// <summary>Kilograms.</summary>
    public static Unit Kilogram { get; } = new("kg");

    /// <summary>Millilitres.</summary>
    public static Unit Millilitre { get; } = new("ml");

    /// <summary>Litres.</summary>
    public static Unit Litre { get; } = new("l");

    /// <summary>Teaspoons.</summary>
    public static Unit Teaspoon { get; } = new("tsp");

    /// <summary>Tablespoons.</summary>
    public static Unit Tablespoon { get; } = new("tbsp");

    /// <summary>Individual items: three onions, two eggs.</summary>
    public static Unit Piece { get; } = new("piece");

    /// <summary>Cloves, as of garlic.</summary>
    public static Unit Clove { get; } = new("clove");

    /// <summary>Bunches, as of parsley.</summary>
    public static Unit Bunch { get; } = new("bunch");

    /// <summary>Slices.</summary>
    public static Unit Slice { get; } = new("slice");

    /// <summary>Tins or cans.</summary>
    public static Unit Can { get; } = new("can");

    /// <summary>Packets.</summary>
    public static Unit Pack { get; } = new("pack");

    /// <summary>A pinch. Deliberately never scaled.</summary>
    public static Unit Pinch { get; } = new("pinch");

    /// <summary>The units every household starts with, in picker order.</summary>
    public static IReadOnlyList<Unit> BuiltIn { get; } =
    [
        Gram, Kilogram, Millilitre, Litre, Teaspoon, Tablespoon,
        Piece, Clove, Bunch, Slice, Can, Pack, Pinch
    ];

    /// <summary>Creates a unit from what somebody wrote: letters and single spaces, so <c>200g</c> is a mistake. A spelt-out built-in (<c>EL</c>) is that built-in; see <see cref="UnitSpellings"/>.</summary>
    /// <param name="code">The unit, built in or not.</param>
    public static Result<Unit> Create(string? code)
    {
        var trimmed = Collapse(code);

        if (trimmed.Length is 0 or > MaxCodeLength || !trimmed.All(IsAllowed))
        {
            return RecipeErrors.InvalidUnit;
        }

        // A built-in is returned as itself, so built-ins stay reference-equal.
        return UnitSpellings.Resolve(trimmed) ?? new Unit(trimmed);
    }

    /// <inheritdoc />
    public bool Equals(Unit? other) =>
        other is not null && Code.Equals(other.Code, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Code);

    /// <inheritdoc />
    public override string ToString() => Code;

    private static bool IsAllowed(char character) =>
        char.IsLetter(character) || character == ' ' || character == '.';

    // Trims and collapses whitespace runs, so "fl  oz" and "fl oz" are one unit.
    private static string Collapse(string? code) =>
        string.Join(
            ' ',
            (code ?? string.Empty).Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

/// <summary>The family a unit belongs to, which decides what it can be added to.</summary>
public enum UnitFamily
{
    /// <summary>No unit at all: "salt", "a little oil".</summary>
    None = 0,

    /// <summary>Grams and kilograms.</summary>
    Mass = 1,

    /// <summary>Millilitres and litres.</summary>
    Volume = 2,

    /// <summary>Teaspoons and tablespoons, deliberately their own family.</summary>
    Spoon = 3,

    /// <summary>Countable things and everything a household added; they only add to the identical unit.</summary>
    Count = 4
}
