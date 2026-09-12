using System.Globalization;
using Domain.Shared;

namespace Api.Infrastructure;

/// <summary>
/// Conditional requests, built on each entity's monotonic version.
/// </summary>
/// <remarks>
/// <para>
/// The ETag is derived from the version rather than from hashing the response
/// body. Hashing costs a full render on every request, changes when an
/// unrelated field's formatting changes, and yields no concurrency token. A
/// version is cheap, stable, and already the thing optimistic concurrency
/// needs.
/// </para>
/// <para>
/// One mechanism solves two problems: <c>If-None-Match</c> avoids resending
/// unchanged data, and <c>If-Match</c> stops two writers clobbering each other.
/// </para>
/// <para>
/// <strong>A version alone identifies a resource only when the URL does.</strong>
/// <c>/users/me</c> means a different person for every session, and two people
/// are both at version 1 on the day they sign up — so a browser holding one
/// person's response would revalidate it, be told 304, and show their data to
/// the next person to sign in on that device. Any route whose meaning depends
/// on who is asking must use the overload that takes the entity's identity.
/// </para>
/// </remarks>
internal static class ETag
{
    private const string Prefix = "v";

    /// <summary>
    /// Formats a version as a strong entity tag, for a URL that already names
    /// the entity.
    /// </summary>
    internal static string Of(long version) =>
        $"\"{Prefix}{version.ToString(CultureInfo.InvariantCulture)}\"";

    /// <summary>
    /// Formats a tag for a URL that does not name the entity it returns, and
    /// whose response may contain more than that entity.
    /// </summary>
    /// <param name="version">The entity's version.</param>
    /// <param name="identity">Which entity this URL resolved to for this caller.</param>
    /// <param name="variant">
    /// Everything else the response body depends on. A tag must change whenever
    /// any part of the body does: <c>/users/me</c> lists the households the
    /// account belongs to, and joining one does not change the account.
    /// </param>
    /// <remarks>
    /// The version stays last, so <see cref="Read"/> can still recover it for
    /// <c>If-Match</c> on a write.
    /// </remarks>
    internal static string Of(long version, Guid identity, string variant = "") =>
        variant.Length == 0
            ? $"\"{identity:N}-{Prefix}{version.ToString(CultureInfo.InvariantCulture)}\""
            : $"\"{identity:N}-{variant}-{Prefix}{version.ToString(CultureInfo.InvariantCulture)}\"";

    /// <summary>
    /// Folds a set of values into a short, stable discriminator for a tag.
    /// </summary>
    /// <remarks>
    /// Order-independent and collision-resistant enough for a validator: the
    /// cost of a collision is one stale read, not a wrong write, and a wrong
    /// write is still stopped by <c>If-Match</c> on the version.
    /// </remarks>
    internal static string Fingerprint(IEnumerable<string> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        var folded = 0UL;

        foreach (var part in parts)
        {
            // FNV-1a over each part, then combined with XOR so the order two
            // households come back in cannot change the tag.
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
    /// Parses a version out of an entity tag, or returns null when the value is
    /// not one this server issued.
    /// </summary>
    internal static long? Read(string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return null;
        }

        var trimmed = headerValue.Trim();

        // Weak validators are accepted on read: a proxy may weaken an ETag it
        // forwards, and the version it carries is still exact.
        if (trimmed.StartsWith("W/", StringComparison.Ordinal))
        {
            trimmed = trimmed[2..];
        }

        trimmed = trimmed.Trim('"');

        // A tag may carry an identity before the version. Only the version is
        // ever compared on a write: the identity is fixed by the route.
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
    /// Returns the body with an <c>ETag</c>, or <c>304</c> with no body when the
    /// caller already has this version.
    /// </summary>
    /// <typeparam name="TBody">The response shape.</typeparam>
    /// <param name="context">The current request.</param>
    /// <param name="body">The response to send when it has changed.</param>
    /// <param name="version">The entity's current version.</param>
    internal static IResult Ok<TBody>(HttpContext context, TBody body, long version) =>
        Respond(context, body, Of(version));

    /// <summary>
    /// The same, for a URL whose meaning depends on who is asking.
    /// </summary>
    /// <typeparam name="TBody">The response shape.</typeparam>
    /// <param name="context">The current request.</param>
    /// <param name="body">The response to send when it has changed.</param>
    /// <param name="version">The entity's current version.</param>
    /// <param name="identity">
    /// The entity this URL resolved to for this caller. Without it, two
    /// entities at the same version share a tag and one caller's cached
    /// response is served to another.
    /// </param>
    /// <param name="variant">
    /// Everything else the body depends on — see <see cref="Of(long, Guid, string)"/>.
    /// </param>
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

        // Private, because a recipe belongs to one household; no-cache, because
        // the client must revalidate rather than reuse a stale copy blindly.
        context.Response.Headers.CacheControl = "private, no-cache";

        // Compared whole, not by version: a tag from a different entity must
        // never match, however its version happens to line up.
        return Matches(context.Request.Headers.IfNoneMatch, tag)
            ? Results.StatusCode(StatusCodes.Status304NotModified)
            : Results.Ok(body);
    }

    /// <summary>
    /// Reads the version a writer claims to be updating.
    /// </summary>
    /// <param name="context">The current request.</param>
    /// <returns>
    /// The version, or a failure: <c>428</c> when the header is absent, so the
    /// client knows to read and retry, and <c>400</c> when it is present but
    /// malformed — which is a client bug, not a stale version.
    /// </returns>
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

    /// <summary>
    /// Whether the caller already holds exactly this tag.
    /// </summary>
    /// <remarks>
    /// String comparison, ignoring only a weak-validator prefix a proxy may
    /// have added. Comparing parsed versions instead is what let one person's
    /// cached response be revalidated into a 304 for another.
    /// </remarks>
    private static bool Matches(IEnumerable<string?> headerValues, string tag) =>
        headerValues.Any(value => Strong(value) == tag);

    private static string? Strong(string? headerValue)
    {
        var trimmed = headerValue?.Trim();

        return trimmed?.StartsWith("W/", StringComparison.Ordinal) == true ? trimmed[2..] : trimmed;
    }
}
