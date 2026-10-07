namespace Application.Abstractions.Settings;

/// <summary>
/// What this instance says about itself to strangers: its public address and where a vulnerability
/// report should go.
/// </summary>
/// <remarks>
/// Both belong to the operator, not the image, so they arrive at runtime. Without a contact there
/// is no security.txt: a contact nobody reads tells a finder they have reported something when they
/// have not.
/// </remarks>
public sealed record SiteSettings
{
    /// <summary>The configuration section these values are read from.</summary>
    public const string SectionName = "Site";

    /// <summary>
    /// The instance's public address, such as <c>https://culina.example.com</c>, or null when it
    /// has none worth publishing.
    /// </summary>
    public Uri? Url { get; init; }

    /// <summary>
    /// Where to report a vulnerability: a <c>mailto:</c>, <c>https://</c> or <c>tel:</c> address
    /// its operator actually reads.
    /// </summary>
    public Uri? SecurityContact { get; init; }

    /// <summary>Throws when any value would make the process unable to serve.</summary>
    public void Validate()
    {
        if (Url is { } url && url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                $"Configuration {SectionName}__{nameof(Url)} must be an http:// or https:// address, but was '{url}'.");
        }

        // RFC 9116: an address for mail, a web page over TLS, or a telephone number.
        if (SecurityContact is { } contact
            && contact.Scheme != Uri.UriSchemeMailto && contact.Scheme != Uri.UriSchemeHttps && contact.Scheme != "tel")
        {
            throw new InvalidOperationException(
                $"Configuration {SectionName}__{nameof(SecurityContact)} must be a mailto:, https:// or tel: address, but was '{contact}'.");
        }
    }
}
