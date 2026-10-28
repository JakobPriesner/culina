namespace Contracts.Products;

/// <summary>
/// A packaged product as Open Food Facts describes it, per 100 g (or 100 ml). Crowd-sourced under the ODbL: show it
/// with its source and <c>url</c>, and keep it only when a person confirms it.
/// </summary>
public sealed record CatalogueProductDetail
{
    /// <summary>The barcode in its normalised form.</summary>
    public required string Gtin { get; init; }

    /// <summary>The product's name, German where it has one.</summary>
    public required string Name { get; init; }

    /// <summary>Its brand.</summary>
    public string? Brand { get; init; }

    /// <summary>What the pack says it holds, as printed: "450 g".</summary>
    public string? Quantity { get; init; }

    /// <summary>The pack's contents in grams or millilitres, when known as a number.</summary>
    public decimal? PackGrams { get; init; }

    /// <summary>One serving in grams or millilitres, when the label names one.</summary>
    public decimal? ServingGrams { get; init; }

    /// <summary>Energy in kilojoules.</summary>
    public decimal? EnergyKj { get; init; }

    /// <summary>Energy in kilocalories.</summary>
    public decimal? EnergyKcal { get; init; }

    /// <summary>Fat in grams.</summary>
    public decimal? Fat { get; init; }

    /// <summary>Saturated fat in grams.</summary>
    public decimal? SaturatedFat { get; init; }

    /// <summary>Carbohydrate in grams.</summary>
    public decimal? Carbohydrate { get; init; }

    /// <summary>Sugars in grams.</summary>
    public decimal? Sugars { get; init; }

    /// <summary>Protein in grams.</summary>
    public decimal? Protein { get; init; }

    /// <summary>Salt in grams.</summary>
    public decimal? Salt { get; init; }

    /// <summary>
    /// What to check before taking it over: <c>noEnergy</c>, <c>macrosMissing</c>, <c>energyMismatch</c> (kcal and
    /// macros disagree, often per-serving values), <c>unitMismatch</c> (kJ and kcal disagree), <c>impossible</c>.
    /// </summary>
    public required IReadOnlyList<string> Doubts { get; init; }

    /// <summary>The product's page at Open Food Facts, for the attribution.</summary>
    public required string Url { get; init; }
}

/// <summary>What an explicit search at Open Food Facts found.</summary>
public sealed record CatalogueProductsResponse
{
    /// <summary>The products, best fit first. Their pack size is not known here: read one by its barcode for that.</summary>
    public required IReadOnlyList<CatalogueProductDetail> Items { get; init; }
}
