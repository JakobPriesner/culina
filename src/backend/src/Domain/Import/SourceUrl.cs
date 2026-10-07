namespace Domain.Import;

/// <summary>The address of a recipe's original, fit to be shown as a link.</summary>
/// <remarks>
/// It comes from places this app does not control and becomes an <c>href</c> (public share page
/// included), so only an absolute <c>http</c>/<c>https</c> address with a host qualifies:
/// <c>javascript://chefkoch.de/%0a…</c> would even be labelled "chefkoch.de". The address as parsed
/// is kept, so shown host and followed address come from one thing.
/// </remarks>
public sealed record SourceUrl
{
    /// <summary>Longer than any recipe page's address, tracking parameters included.</summary>
    public const int MaxLength = 2048;

    private SourceUrl(Uri address) => Value = address.AbsoluteUri;

    /// <summary>The address, as parsed.</summary>
    public string Value { get; }

    /// <summary>
    /// Reads an address, or says there is none worth linking to (not an http or https one with a
    /// host).
    /// </summary>
    public static SourceUrl? From(string? text)
    {
        var trimmed = text?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxLength
            || !Uri.TryCreate(trimmed, UriKind.Absolute, out var address))
        {
            return null;
        }

        return address.Scheme is "http" or "https" && address.Host.Length > 0
            ? new SourceUrl(address)
            : null;
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
