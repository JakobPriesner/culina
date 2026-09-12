namespace Domain.Recipes;

/// <summary>
/// The units an ingredient amount can be given in.
/// </summary>
/// <remarks>
/// A closed set, surfaced through OpenAPI so the frontend shares one
/// definition rather than redeclaring the vocabulary. Values travel as
/// lowercase strings.
/// </remarks>
public enum Unit
{
    /// <summary>Grams.</summary>
    Gram = 0,

    /// <summary>Kilograms.</summary>
    Kilogram = 1,

    /// <summary>Millilitres.</summary>
    Millilitre = 2,

    /// <summary>Litres.</summary>
    Litre = 3,

    /// <summary>Teaspoons.</summary>
    Teaspoon = 4,

    /// <summary>Tablespoons.</summary>
    Tablespoon = 5,

    /// <summary>Individual items: three onions, two eggs.</summary>
    Piece = 6,

    /// <summary>Cloves, as of garlic.</summary>
    Clove = 7,

    /// <summary>Bunches, as of parsley.</summary>
    Bunch = 8,

    /// <summary>Slices.</summary>
    Slice = 9,

    /// <summary>Tins or cans.</summary>
    Can = 10,

    /// <summary>Packets.</summary>
    Pack = 11,

    /// <summary>A pinch. Deliberately never scaled.</summary>
    Pinch = 12
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

    /// <summary>Countable things, which only add to the identical unit.</summary>
    Count = 4
}
