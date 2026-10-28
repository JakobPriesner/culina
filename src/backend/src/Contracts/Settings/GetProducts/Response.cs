namespace Contracts.Settings.GetProducts;

/// <summary>Whether this server looks barcodes up at Open Food Facts.</summary>
public sealed record Response
{
    /// <summary>Whether a barcode nobody named, and an explicit name search, are asked at Open Food Facts.</summary>
    public required bool OpenFoodFacts { get; init; }

    /// <summary>The address sent in the User-Agent so Open Food Facts can reach whoever runs this server.</summary>
    public string? Contact { get; init; }
}
