using Domain.Sessions;
using Domain.Shared;

namespace Api.Infrastructure;

/// <summary>
/// Gives framework-generated statuses a problem document body.
/// </summary>
/// <remarks>
/// <para>
/// A 404 from an unmatched route, a 405 from a wrong method and a 401 from an
/// authentication challenge are produced by the framework, not by a handler, so
/// they would otherwise be the only API responses with an empty body. The
/// frontend parses one error format, so they get one too.
/// </para>
/// <para>
/// The original status is always preserved. Deriving it from the error type
/// here would rewrite every one of them — a 401 challenge would arrive as a
/// 500 — which is exactly the bug this comment exists to prevent recurring.
/// </para>
/// <para>
/// Non-API paths are left alone: the app shell owns those, and a browser asking
/// for a client route should not be handed JSON it would render as text.
/// </para>
/// </remarks>
internal static class StatusCodeProblems
{
    private static readonly Error NoSuchEndpoint = new(
        "request.no_such_endpoint",
        "There is nothing at this address.",
        ErrorType.NotFound);

    private static readonly Error MethodNotAllowed = new(
        "request.method_not_allowed",
        "That method is not supported at this address.",
        ErrorType.Validation);

    private static readonly Error UnsupportedMediaType = new(
        "request.unsupported_media_type",
        "That content type is not supported at this address.",
        ErrorType.Validation);

    private static readonly Error Rejected = new(
        "request.rejected",
        "The request could not be handled.",
        ErrorType.Failure);

    internal static Task WriteAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!ApiPaths.IsApi(context.Request.Path))
        {
            return Task.CompletedTask;
        }

        var status = context.Response.StatusCode;

        var error = status switch
        {
            StatusCodes.Status401Unauthorized => SessionErrors.NotAuthenticated,
            StatusCodes.Status403Forbidden => RequestErrors.Forbidden,
            StatusCodes.Status404NotFound => NoSuchEndpoint,
            StatusCodes.Status405MethodNotAllowed => MethodNotAllowed,
            StatusCodes.Status415UnsupportedMediaType => UnsupportedMediaType,
            _ => Rejected
        };

        return CustomResults.WriteProblemAsync(context, error, status);
    }
}
