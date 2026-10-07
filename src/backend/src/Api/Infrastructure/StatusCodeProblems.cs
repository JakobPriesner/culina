using Domain.Sessions;
using Domain.Shared;

namespace Api.Infrastructure;

/// <summary>
/// Gives framework-generated statuses (404, 405, 401 challenges) a problem document body.
/// The original status is preserved, and non-API paths are left to the app shell.
/// </summary>
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
