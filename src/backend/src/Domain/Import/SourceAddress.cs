using Domain.Shared;

namespace Domain.Import;

/// <summary>The address of another app, reduced to scheme, host and port.</summary>
/// <remarks>
/// People paste whatever is in their address bar; everything after the authority is dropped, or a
/// pasted <c>/api</c> would make every request <c>/api/api/recipe/</c>. Reachability is decided at
/// connect time (<see cref="PublicAddress"/>).
/// </remarks>
public sealed record SourceAddress
{
    /// <summary>Longer than any host anyone has ever typed.</summary>
    public const int MaxLength = 200;

    private SourceAddress(Uri origin) => Origin = origin;

    /// <summary>The address, as scheme, host and port and nothing else.</summary>
    public Uri Origin { get; }

    /// <summary>How it is stored and shown: no trailing slash.</summary>
    public string Value => Origin.GetLeftPart(UriPartial.Authority);

    /// <summary>Reads an address somebody typed, or refuses it.</summary>
    public static Result<SourceAddress> Create(string? typed)
    {
        var text = typed?.Trim() ?? string.Empty;

        if (text.Length is 0 or > MaxLength)
        {
            return ImportErrors.InvalidSourceAddress;
        }

        // A bare host is the common paste; https when unsaid, since this request carries a
        // credential.
        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = $"https://{text}";
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var parsed))
        {
            return ImportErrors.InvalidSourceAddress;
        }

        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
        {
            return ImportErrors.InvalidSourceAddress;
        }

        // Only an empty host is refused: "tandoor:8080" is a real home-network name.
        return string.IsNullOrEmpty(parsed.Host)
            ? ImportErrors.InvalidSourceAddress
            : new SourceAddress(parsed);
    }

    /// <summary>Builds a path on this address from a rooted path and query.</summary>
    public Uri At(string relative) => new(Origin, relative);

    /// <inheritdoc />
    public override string ToString() => Value;
}
