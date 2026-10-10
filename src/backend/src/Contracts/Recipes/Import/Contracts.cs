namespace Contracts.Recipes.Import;

/// <summary>Asks for a recipe to be read from a web page.</summary>
public sealed record Request
{
    /// <summary>The address. An ordinary public http or https page.</summary>
    public required string Url { get; init; }
}

/// <summary>What a page turned out to say: a draft, never a recipe.</summary>
/// <remarks>
/// Nothing is created; it is shown back for correction and the person decides what to keep.
/// </remarks>
public sealed record Response
{
    /// <summary>Where it was read from, after any redirects.</summary>
    public required string SourceUrl { get; init; }

    /// <summary>What the page called it.</summary>
    public string? Title { get; init; }

    /// <summary>Its ingredients, one line each, exactly as the page wrote them.</summary>
    public required IReadOnlyList<string> IngredientLines { get; init; }

    /// <summary>Its instructions, one paragraph each.</summary>
    public required IReadOnlyList<string> Steps { get; init; }

    /// <summary>How many it says it makes, when it gave a number; the larger end of a range.</summary>
    public decimal? Servings { get; init; }

    /// <summary>Whether that is "servings" or "pieces", when it gave a number.</summary>
    public string? YieldKind { get; init; }

    /// <summary>The page's own word for what it makes ("Muffins", "Kuchen (26 cm)"), when it named one.</summary>
    public string? YieldLabel { get; init; }

    /// <summary>How long it takes, when the page said.</summary>
    public int? TotalMinutes { get; init; }

    /// <summary>The page's words, when it published no structured data.</summary>
    /// <remarks>
    /// The fallback: the client reads it with the same parser as a pasted recipe, so there is one
    /// set of heuristics, on the side of whoever is correcting it.
    /// </remarks>
    public string? Text { get; init; }

    /// <summary>Written caption or description, when published separately.</summary>
    public string? Caption { get; init; }

    /// <summary>Public speech captions, when the source makes them available.</summary>
    public string? Transcript { get; init; }
}
