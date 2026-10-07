namespace Domain.Import;

/// <summary>
/// The address of a recipe's original, fit to be shown as a link.
/// </summary>
/// <remarks>
/// <para>
/// It arrives from places this app does not control — a connected Tandoor's
/// <c>source_url</c>, the page a pasted link redirected to, a share sheet, a
/// request body — and it ends up as the <c>href</c> on every reading of the
/// recipe, the public share page included, labelled with its host.
/// </para>
/// <para>
/// So only an absolute <c>http</c> or <c>https</c> address with a host is one.
/// <c>javascript:</c>, <c>data:</c>, <c>file:</c> and the schemes desktop apps
/// register for themselves (<c>search-ms:</c>, <c>ms-officecmd:</c>) are not
/// places a link on a recipe should go, and
/// <c>javascript://chefkoch.de/%0a…</c> would even be labelled "chefkoch.de".
/// What is kept is the address as parsed, so the host shown and the address
/// followed are read from the same thing.
/// </para>
/// </remarks>
public sealed record SourceUrl
{
    /// <summary>Longer than any recipe page's address, tracking parameters included.</summary>
    public const int MaxLength = 2048;

    private SourceUrl(Uri address) => Value = address.AbsoluteUri;

    /// <summary>The address, as parsed.</summary>
    public string Value { get; }

    /// <summary>Reads an address, or says there is none worth linking to.</summary>
    /// <param name="text">Whatever came in as the original's address.</param>
    /// <returns>The address, or null if it is not an http or https one with a host.</returns>
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
