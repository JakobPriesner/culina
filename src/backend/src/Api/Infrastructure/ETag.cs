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
/// </remarks>
internal static class ETag
{
    private const string Prefix = "v";

    /// <summary>Formats a version as a strong entity tag.</summary>
    internal static string Of(long version) =>
        $"\"{Prefix}{version.ToString(CultureInfo.InvariantCulture)}\"";

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

        if (!trimmed.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        return long.TryParse(trimmed[Prefix.Length..], CultureInfo.InvariantCulture, out var version)
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
    internal static IResult Ok<TBody>(HttpContext context, TBody body, long version)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.Headers.ETag = Of(version);

        // Private, because a recipe belongs to one household; no-cache, because
        // the client must revalidate rather than reuse a stale copy blindly.
        context.Response.Headers.CacheControl = "private, no-cache";

        return HasVersion(context.Request.Headers.IfNoneMatch, version)
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

    private static bool HasVersion(IEnumerable<string?> headerValues, long version) =>
        headerValues.Any(value => Read(value) == version);
}
