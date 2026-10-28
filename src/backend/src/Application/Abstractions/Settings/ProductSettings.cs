namespace Application.Abstractions.Settings;

/// <summary>
/// Whether barcodes are looked up at Open Food Facts. Mutable: an admin changes it and every holder of the singleton
/// sees it at once.
/// </summary>
/// <remarks>
/// On by default since 28 October 2026 (Jakob): a scan should find the product, and typing a label is for what Open
/// Food Facts does not know. Each new barcode and each explicit name search then leaves the server for
/// openfoodfacts.org; an administrator can switch it off (<c>docs/barcode-scanning-design.md</c> §5.2).
/// </remarks>
public sealed record ProductSettings : IInstanceSettings<ProductSettings>
{
    /// <summary>The key this group is stored under.</summary>
    public static string GroupName => "products";

    /// <summary>Culina's own address, sent in the User-Agent when the administrator gave no contact of their own.</summary>
    public const string ProjectAddress = "https://github.com/JakobPriesner/culina";

    /// <summary>Whether a barcode nobody named is looked up at Open Food Facts.</summary>
    public bool OpenFoodFacts { get; set; } = true;

    /// <summary>
    /// How Open Food Facts can reach whoever runs this server, sent in the User-Agent as its API asks: an email
    /// address or a web address. Optional: without one, Culina names its project page, so nothing personal is sent.
    /// </summary>
    public string? Contact { get; set; }

    /// <summary>Whether the lookup may run.</summary>
    public bool LooksUp => OpenFoodFacts;

    /// <summary>What the User-Agent names as the way to reach this server.</summary>
    public string ContactOrProject => string.IsNullOrWhiteSpace(Contact) ? ProjectAddress : Contact;

    /// <inheritdoc/>
    public void CopyFrom(ProductSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);

        OpenFoodFacts = other.OpenFoodFacts;
        Contact = other.Contact;
    }
}
