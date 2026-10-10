namespace Domain.Nutrition;

/// <summary>Which part of a hen's egg a food is, so an egg counted in pieces can be weighed.</summary>
public enum EggPart
{
    /// <summary>Not an egg.</summary>
    None = 0,

    /// <summary>The whole egg without its shell.</summary>
    Whole = 1,

    /// <summary>The yolk.</summary>
    Yolk = 2,

    /// <summary>The white.</summary>
    White = 3
}
