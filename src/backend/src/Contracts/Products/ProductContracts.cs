namespace Contracts.Products;

/// <summary>What a household says a barcode is.</summary>
public sealed record SetProductRequest
{
    /// <summary>What to show: "Barilla Spaghetti n.5". Required, at most 200 characters.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The ingredient a recipe line or list item is matched by: "Spaghetti". Blank is none; at most 200 characters.</summary>
    public string? Ingredient { get; init; }

    /// <summary>What the pack holds, more than 0 and at most 9999999.</summary>
    public decimal? Quantity { get; init; }

    /// <summary>The unit of <c>quantity</c>, written as in a recipe (<c>g</c>, <c>ml</c>, <c>pack</c>); needs a quantity.</summary>
    public string? Unit { get; init; }

    /// <summary>Minutes the household found it needs, 1 to 600. A barcode never supplies this; only the household does.</summary>
    public int? CookMinutes { get; init; }

    /// <summary>A line for people, at most 500 characters.</summary>
    public string? Note { get; init; }
}

/// <summary>What a household says a barcode is, as the household's memory holds it.</summary>
public sealed record ProductDetail
{
    /// <summary>The barcode in its normalised form: digits only, a UPC-A padded to 13.</summary>
    public required string Gtin { get; init; }

    /// <summary>What to show.</summary>
    public required string Name { get; init; }

    /// <summary>The ingredient a recipe line or list item is matched by.</summary>
    public string? Ingredient { get; init; }

    /// <summary>What the pack holds.</summary>
    public decimal? Quantity { get; init; }

    /// <summary>The unit of <c>quantity</c>.</summary>
    public string? Unit { get; init; }

    /// <summary>Minutes the household found it needs.</summary>
    public int? CookMinutes { get; init; }

    /// <summary>A line for people.</summary>
    public string? Note { get; init; }

    /// <summary>
    /// The household whose memory this is: the one asked, or the nearest household it inherits from. Only that
    /// household changes or removes it.
    /// </summary>
    public required Guid HouseholdId { get; init; }

    /// <summary>The version of the row, sent back as <c>If-Match</c> to change or remove it.</summary>
    public required long Version { get; init; }

    /// <summary>
    /// The household food kept for this barcode with its values per 100 g, nearest household first; null when nobody
    /// typed or took over its label yet. Only the single read carries it, not the list.
    /// </summary>
    public Nutrition.HouseholdFoodDetail? Food { get; init; }
}

/// <summary>Every barcode a household, and the households it inherits from, have named.</summary>
public sealed record ProductsResponse
{
    /// <summary>The products by barcode; a barcode more than one household named is the nearest household's.</summary>
    public required IReadOnlyList<ProductDetail> Items { get; init; }
}
