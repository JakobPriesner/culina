using Domain.Shared;

namespace Api.Infrastructure;

/// <summary>Failures that are facts about the wire (malformed header, missing precondition, rejected origin), following the same <c>module.reason</c> codes as domain errors.</summary>
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
        $"Unknown query parameter '{name}'. Check the spelling against the API documentation.",
        ErrorType.Validation);

    internal static Error MissingQueryParameter(string name) => new FieldError(
        name,
        "request.missing_parameter",
        $"Query parameter '{name}' is required.");

    internal static Error InvalidQueryParameter(string name, string expected) => new FieldError(
        name,
        "request.invalid_parameter",
        $"Query parameter '{name}' must be {expected}.");

    internal static Error RepeatedQueryParameter(string name) => new(
        "request.repeated_parameter",
        $"Query parameter '{name}' may only be given once.",
        ErrorType.Validation);

    internal static readonly Error Forbidden = new(
        "request.forbidden",
        "You do not have access to that.",
        ErrorType.Forbidden);

    internal static readonly Error ForeignOrigin = new(
        "auth.foreign_origin",
        "This request did not come from the application.",
        ErrorType.Forbidden);

    internal static readonly Error RateLimited = new(
        "request.rate_limited",
        "Too many requests. Wait a moment and try again.",
        ErrorType.RateLimited);

}
