using System.Globalization;
using Domain.Shared;

namespace Domain.Recipes;

/// <summary>
/// What an ingredient amount is measured in.
/// </summary>
/// <remarks>
/// <para>
/// A code rather than an enum, because the vocabulary is open. Thirteen units
/// are built in and every household starts with them; a cook who measures in
/// <c>Schuss</c>, <c>Handvoll</c> or <c>Becher</c> adds one by writing it, and
/// that is the whole of adding a unit. No screen, no list to maintain, and no
/// way for a catalogue to disagree with what the recipes actually say.
/// </para>
/// <para>
/// What a household adds is a <see cref="UnitFamily.Count"/> unit: it scales
/// with the portions and it sums with itself, but it never converts to grams or
/// millilitres. Nobody knows how much a Schuss is, and a shopping list that
/// claimed to would be inventing the number. See <see cref="Units"/>.
/// </para>
/// <para>
/// Compared case-insensitively so <c>Schuss</c> and <c>schuss</c> are one unit,
/// and stored as it was written so a German noun keeps its capital letter.
/// </para>
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

    /// <summary>
    /// The units every household starts with, in the order a picker offers
    /// them.
    /// </summary>
    public static IReadOnlyList<Unit> BuiltIn { get; } =
    [
        Gram, Kilogram, Millilitre, Litre, Teaspoon, Tablespoon,
        Piece, Clove, Bunch, Slice, Can, Pack, Pinch
    ];

    /// <summary>Creates a unit from what somebody wrote.</summary>
    /// <param name="code">The unit, built in or not.</param>
    /// <remarks>
    /// <para>
    /// Letters and single spaces, so <c>Schuss</c> and <c>fl oz</c> are units
    /// and <c>200g</c> is a mistake — an amount that lost its space, which
    /// would otherwise become a unit nobody could ever match again.
    /// </para>
    /// <para>
    /// A built-in written out — <c>Milliliter</c>, <c>EL</c> — is that built-in,
    /// not a new unit. See <see cref="UnitSpellings"/>.
    /// </para>
    /// </remarks>
    public static Result<Unit> Create(string? code)
    {
        var trimmed = Collapse(code);

        if (trimmed.Length is 0 or > MaxCodeLength || !trimmed.All(IsAllowed))
        {
            return RecipeErrors.InvalidUnit;
        }

        // A built-in is returned as itself, however it was spelt, so the
        // built-ins stay reference-equal and a `switch` over them in a test
        // reads the way it looks.
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

    /// <summary>
    /// Trims, and makes any run of whitespace one space.
    /// </summary>
    /// <remarks>
    /// "fl  oz" and "fl oz" have to be the same unit, or a shopping list ends
    /// up with two lines whose difference nobody can see.
    /// </remarks>
    private static string Collapse(string? code) =>
        string.Join(
            ' ',
            (code ?? string.Empty).Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}

/// <summary>
/// The family a unit belongs to, which decides what it can be added to.
/// </summary>
public enum UnitFamily
{
    /// <summary>No unit at all: "salt", "a little oil".</summary>
    None = 0,

    /// <summary>Grams and kilograms.</summary>
    Mass = 1,

    /// <summary>Millilitres and litres.</summary>
    Volume = 2,

    /// <summary>
    /// Teaspoons and tablespoons, which are deliberately their own family.
    /// </summary>
    Spoon = 3,

    /// <summary>
    /// Countable things, and everything a household added itself. They only
    /// add to the identical unit.
    /// </summary>
    Count = 4
}
