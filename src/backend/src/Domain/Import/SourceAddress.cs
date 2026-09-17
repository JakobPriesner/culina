using Domain.Shared;

namespace Domain.Import;

/// <summary>
/// The address of another app, reduced to the only part of it worth keeping.
/// </summary>
/// <remarks>
/// <para>
/// People paste what is in their browser's address bar, which is why this
/// exists: <c>https://tandoor.example.com/search/?page=3</c>,
/// <c>tandoor.example.com</c>, and the same with a trailing slash are one
/// address, and storing them as three would mean three connections to one
/// instance. Everything after the authority is dropped — a path kept here is a
/// path prefixed onto every request this connection ever makes, and the first
/// person to paste <c>/api</c> would get <c>/api/api/recipe/</c> forever.
/// </para>
/// <para>
/// This says nothing about whether the address may be <em>reached</em>. That is
/// a question about the network the server is standing in, it needs a DNS
/// lookup to answer, and it is answered at the moment of connecting rather than
/// here. See <see cref="PublicAddress"/>.
/// </para>
/// </remarks>
public sealed record SourceAddress
{
    /// <summary>Longer than any host anyone has ever typed.</summary>
    public const int MaxLength = 200;

    private SourceAddress(Uri origin) => Origin = origin;

    /// <summary>The address, as scheme, host and port and nothing else.</summary>
    public Uri Origin { get; }

    /// <summary>How it is stored and shown: no trailing slash, so it reads as an address.</summary>
    public string Value => Origin.GetLeftPart(UriPartial.Authority);

    /// <summary>Reads an address somebody typed, or refuses it.</summary>
    /// <param name="typed">What they pasted, with or without a scheme.</param>
    public static Result<SourceAddress> Create(string? typed)
    {
        var text = typed?.Trim() ?? string.Empty;

        if (text.Length is 0 or > MaxLength)
        {
            return ImportErrors.InvalidSourceAddress;
        }

        // A bare host is what most people paste, and refusing it would be
        // refusing the common case on a technicality. https, never http, when
        // they did not say: the token in this request is a credential.
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

        // A host with no dot and no port is almost always a typo rather than an
        // intranet name — but not always, and "tandoor:8080" is a real thing on
        // a real home network, so only the empty host is refused here.
        return string.IsNullOrEmpty(parsed.Host)
            ? ImportErrors.InvalidSourceAddress
            : new SourceAddress(parsed);
    }

    /// <summary>Builds a path on this address.</summary>
    /// <param name="relative">A path and query, rooted: <c>/api/recipe/</c>.</param>
    public Uri At(string relative) => new(Origin, relative);

    /// <inheritdoc />
    public override string ToString() => Value;
}
