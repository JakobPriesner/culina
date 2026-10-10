using System.Globalization;
using Domain.Shared;
using Microsoft.Extensions.Primitives;

namespace Api.Infrastructure;

/// <summary>
/// The one way an endpoint reads a typed query parameter. Absent is <c>request.missing_parameter</c> when the
/// parameter is required; present but wrong is always <c>request.invalid_parameter</c>, never coerced or clamped,
/// so a client bug such as <c>limit=twenty</c> or <c>limit=500</c> is not hidden.
/// </summary>
/// <remarks>
/// Endpoints read from <see cref="IQueryCollection"/> rather than binding typed lambda parameters, because the
/// framework's binder answers a malformed value with a generic 400 before any endpoint code runs. A present
/// parameter with an empty value (<c>limit=</c>) is invalid, not absent.
/// </remarks>
internal static class QueryParameters
{
    /// <summary>A required identifier.</summary>
    internal static Result<Guid> RequireGuid(this IQueryCollection query, string name) =>
        query.ReadGuid(name).Bind(given =>
            given.Value is { } id ? Result<Guid>.Success(id) : RequestErrors.MissingQueryParameter(name));

    /// <summary>An optional identifier; null when absent.</summary>
    internal static Result<Optional<Guid>> ReadGuid(this IQueryCollection query, string name) =>
        Read(query, name, "an id", static (string text, out Guid value) => Guid.TryParse(text, CultureInfo.InvariantCulture, out value));

    /// <summary>Every value of a repeatable identifier parameter, in order.</summary>
    internal static Result<IReadOnlyList<Guid>> ReadGuids(this IQueryCollection query, string name)
    {
        ArgumentNullException.ThrowIfNull(query);

        List<Guid> ids = [];

        foreach (var text in query[name])
        {
            if (!Guid.TryParse(text, CultureInfo.InvariantCulture, out var id))
            {
                return RequestErrors.InvalidQueryParameter(name, "an id");
            }

            ids.Add(id);
        }

        return ids;
    }

    /// <summary>An optional whole number within <paramref name="min"/> and <paramref name="max"/>; null when absent.</summary>
    internal static Result<Optional<int>> ReadInt(this IQueryCollection query, string name, int min, int max = int.MaxValue) =>
        Read(
            query,
            name,
            max == int.MaxValue
                ? $"a whole number of at least {min}"
                : $"a whole number from {min} to {max}",
            (string text, out int value) =>
                int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value)
                && value >= min
                && value <= max);

    /// <summary>A whole number within bounds, or <paramref name="fallback"/> when absent.</summary>
    internal static Result<int> ReadInt(this IQueryCollection query, string name, int min, int max, int fallback) =>
        query.ReadInt(name, min, max).Map(given => given.Value ?? fallback);

    /// <summary>A flag, <c>true</c> or <c>false</c> in any case; <paramref name="fallback"/> when absent.</summary>
    internal static Result<bool> ReadBool(this IQueryCollection query, string name, bool fallback = false) =>
        Read(query, name, "'true' or 'false'", static (string text, out bool value) => bool.TryParse(text, out value))
            .Map(given => given.Value ?? fallback);

    /// <summary>An optional calendar day written <c>yyyy-MM-dd</c>, whatever the server's culture; null when absent.</summary>
    internal static Result<Optional<DateOnly>> ReadDate(this IQueryCollection query, string name) =>
        Read(
            query,
            name,
            "a day written yyyy-MM-dd",
            static (string text, out DateOnly value) =>
                DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value));

    private delegate bool Parser<T>(string text, out T value);

    private static Result<Optional<T>> Read<T>(IQueryCollection query, string name, string expected, Parser<T> parse)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(query);

        StringValues values = query[name];

        if (values.Count == 0)
        {
            return default(Optional<T>);
        }

        // Endpoints that allow a repeat read it with ReadGuids; the guard middleware has already refused the rest.
        return values.Count == 1 && parse(values[0]!, out var value)
            ? Result<Optional<T>>.Success(new Optional<T>(value))
            : RequestErrors.InvalidQueryParameter(name, expected);
    }

    /// <summary>A parameter that may be absent: a result carries a value or an error, and "nothing asked" is neither.</summary>
    /// <typeparam name="T">The kind of value.</typeparam>
    /// <param name="Value">What was given, or null when the parameter was absent.</param>
    internal readonly record struct Optional<T>(T? Value)
        where T : struct;
}
