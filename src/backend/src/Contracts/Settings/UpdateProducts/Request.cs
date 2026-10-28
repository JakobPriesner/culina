namespace Contracts.Settings.UpdateProducts;

/// <summary>Whether to look barcodes up at Open Food Facts.</summary>
public sealed record Request
{
    /// <summary>Whether a barcode nobody named, and an explicit name search, are asked at Open Food Facts.</summary>
    public required bool OpenFoodFacts { get; init; }

    /// <summary>An email or web address for the User-Agent, as Open Food Facts asks, at most 200 characters; without one, Culina's project page is sent.</summary>
    public string? Contact { get; init; }
}
