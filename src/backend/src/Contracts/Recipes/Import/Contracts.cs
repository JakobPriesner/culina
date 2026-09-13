namespace Contracts.Recipes.Import;

/// <summary>Asks for a recipe to be read from a web page.</summary>
public sealed record Request
{
    /// <summary>The address. An ordinary public http or https page.</summary>
    public required string Url { get; init; }
}

/// <summary>
/// What a page turned out to say.
/// </summary>
/// <remarks>
/// A draft, never a recipe. Nothing is created: this is shown back for
/// correction, and the person decides what to keep — which is what keeps an
/// import an import rather than a scraper.
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

    /// <summary>What it says it makes, when that was a plain number.</summary>
    public decimal? Servings { get; init; }

    /// <summary>How long it takes, when the page said.</summary>
    public int? TotalMinutes { get; init; }

    /// <summary>
    /// The page's words, when it published no structured data.
    /// </summary>
    /// <remarks>
    /// The fallback. The client reads this with the same parser it uses for a
    /// pasted recipe, so there is one set of heuristics rather than two — and
    /// it lives on the client, where the person correcting it is.
    /// </remarks>
    public string? Text { get; init; }
}
