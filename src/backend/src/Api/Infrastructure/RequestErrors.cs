using Domain.Shared;

namespace Api.Infrastructure;

/// <summary>
/// Failures that are facts about the wire rather than about the domain:
/// a malformed header, a missing precondition, a rejected origin.
/// </summary>
/// <remarks>
/// They live in <c>Api</c> because nothing below it knows that HTTP exists, but
/// they follow the same <c>module.reason</c> code contract as every domain
/// error, so a client branches on them identically.
/// </remarks>
internal static class RequestErrors
{
    internal static readonly Error PreconditionRequired = new(
        "request.precondition_required",
        "This change requires an If-Match header. Read the resource and retry with its ETag.",
        ErrorType.PreconditionRequired);

    internal static readonly Error MalformedIfMatch = new(
        "request.malformed_if_match",
        "The If-Match header is not an ETag this server issued.",
        ErrorType.Validation);

    internal static Error UnknownQueryParameter(string name) => new(
        "request.unknown_parameter",
        $"'{name}' is not a parameter this endpoint accepts.",
        ErrorType.Validation);

    internal static Error RepeatedQueryParameter(string name) => new(
        "request.repeated_parameter",
        $"'{name}' may only be given once.",
        ErrorType.Validation);

    internal static readonly Error ForeignOrigin = new(
        "auth.foreign_origin",
        "This request did not come from the application.",
        ErrorType.Forbidden);

    internal static readonly Error RateLimited = new(
        "request.rate_limited",
        "Too many requests. Wait a moment and try again.",
        ErrorType.RateLimited);

    internal static readonly Error VersionMismatch = new(
        "request.version_mismatch",
        "This item changed since you loaded it. Reload it and try again.",
        ErrorType.PreconditionFailed);
}
