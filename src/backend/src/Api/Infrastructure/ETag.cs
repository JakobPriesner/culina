using System.Globalization;
using Domain.Shared;

namespace Api.Infrastructure;

/// <summary>Conditional requests, built on each entity's monotonic version.</summary>
/// <remarks>
/// The tag derives from the version, not a body hash: cheaper, stable, and already what
/// <c>If-Match</c> needs. <c>If-None-Match</c> skips unchanged data. A version identifies a
/// resource only when the URL does: a route whose meaning depends on the caller (<c>/users/me</c>)
/// must use the overload taking the entity identity, or one person's cached response is revalidated
/// for the next.
/// </remarks>
internal static class ETag
{
    private const string Prefix = "v";

    /// <summary>
    /// Formats a version as a strong entity tag, for a URL that already names the entity.
    /// </summary>
    internal static string Of(long version) =>
        $"\"{Prefix}{version.ToString(CultureInfo.InvariantCulture)}\"";

    /// <summary>Formats a tag for a URL that does not name the entity it returns.</summary>
    /// <param name="version">The entity's version.</param>
    /// <param name="identity">Which entity this URL resolved to for this caller.</param>
    /// <param name="variant">
    /// Everything else the body depends on, so the tag changes whenever any part of it does.
    /// </param>
    /// <remarks>
    /// The version stays last so <see cref="Read"/> can recover it for <c>If-Match</c>.
    /// </remarks>
    internal static string Of(long version, Guid identity, string variant = "") =>
        variant.Length == 0
            ? $"\"{identity:N}-{Prefix}{version.ToString(CultureInfo.InvariantCulture)}\""
            : $"\"{identity:N}-{variant}-{Prefix}{version.ToString(CultureInfo.InvariantCulture)}\"";

    /// <summary>
    /// Folds a set of values into a short, order-independent discriminator for a tag.
    /// </summary>
    /// <remarks>
    /// A collision costs one stale read, not a wrong write: <c>If-Match</c> still guards the
    /// version.
    /// </remarks>
    internal static string Fingerprint(IEnumerable<string> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        var folded = 0UL;

        foreach (var part in parts)
        {
            // FNV-1a per part, combined with XOR so the order households come back in cannot change
            // the tag.
            var hash = 14695981039346656037UL;

            foreach (var character in part)
            {
                hash = (hash ^ character) * 1099511628211UL;
            }

            folded ^= hash;
        }

        return folded.ToString("x8", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses a version out of an entity tag, or null when the value is not one this server issued.
    /// </summary>
    internal static long? Read(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return null;
        }

        var trimmed = headerValue.Trim();

        // Weak validators are accepted: a proxy may weaken a tag it forwards, and the version is
        // still exact.
        if (trimmed.StartsWith("W/", StringComparison.Ordinal))
        {
            trimmed = trimmed[2..];
        }

        trimmed = trimmed.Trim('"');

        // Only the version is compared on a write; the identity is fixed by the route.
        var lastSegment = trimmed[(trimmed.LastIndexOf('-') + 1)..];

        if (!lastSegment.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        return long.TryParse(lastSegment[Prefix.Length..], CultureInfo.InvariantCulture, out var version)
            ? version
            : null;
    }

    /// <summary>
    /// Returns the body with an <c>ETag</c>, or <c>304</c> with no body when the caller already has
    /// this version.
    /// </summary>
    internal static IResult Ok<TBody>(HttpContext context, TBody body, long version) =>
        Respond(context, body, Of(version));

    /// <summary>The same, for a URL whose meaning depends on who is asking.</summary>
    internal static IResult Ok<TBody>(
        HttpContext context,
        TBody body,
        long version,
        Guid identity,
        string variant = "") =>
        Respond(context, body, Of(version, identity, variant));

    private static IResult Respond<TBody>(HttpContext context, TBody body, string tag)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.Headers.ETag = tag;

        // Private, because a recipe belongs to one household; no-cache, so the client revalidates.
        context.Response.Headers.CacheControl = "private, no-cache";

        // Compared whole, not by version: a tag from a different entity must never match.
        return Matches(context.Request.Headers.IfNoneMatch, tag)
            ? Results.StatusCode(StatusCodes.Status304NotModified)
            : Results.Ok(body);
    }

    /// <summary>
    /// Reads the version a writer claims to be updating: <c>428</c> when <c>If-Match</c> is absent,
    /// <c>400</c> when malformed.
    /// </summary>
    internal static Result<long> RequireIfMatch(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var header = context.Request.Headers.IfMatch;

        if (header.Count == 0)
        {
            return RequestErrors.PreconditionRequired;
        }

        return Read(header[0]) is { } version
            ? version
            : RequestErrors.MalformedIfMatch;
    }

    /// <summary>Whether the caller already holds exactly this tag.</summary>
    /// <remarks>
    /// Whole-string comparison, ignoring a weak prefix a proxy may add: comparing parsed versions
    /// let one person's cached response revalidate into a 304 for another. Internal because image
    /// serving, outside <see cref="Respond"/>, shares the conditional half.
    /// </remarks>
    internal static bool Matches(IEnumerable<string?> headerValues, string tag) =>
        headerValues.Any(value => Strong(value) == tag);

    private static string? Strong(string? headerValue)
    {
        var trimmed = headerValue?.Trim();

        return trimmed?.StartsWith("W/", StringComparison.Ordinal) == true ? trimmed[2..] : trimmed;
    }
}
