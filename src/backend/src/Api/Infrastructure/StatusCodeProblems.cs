using Domain.Shared;

namespace Api.Infrastructure;

/// <summary>
/// Gives framework-generated statuses a problem document body.
/// </summary>
/// <remarks>
/// A 404 from an unmatched route and a 405 from a wrong method are produced by
/// routing, not by a handler, so they would otherwise be the only API responses
/// with an empty body. The frontend parses one error format, so they get one
/// too. Non-API paths are left alone: the app shell owns those.
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

    private static readonly Error Unhandled = new(
        "request.rejected",
        "The request could not be handled.",
        ErrorType.Failure);

    internal static Task WriteAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!ApiPaths.IsApi(context.Request.Path))
        {
            // The problem document is the API's error format. A browser asking
            // for a client route that the app shell should have answered gets
            // the plain status, not JSON it would render as text.
            return Task.CompletedTask;
        }

        var error = context.Response.StatusCode switch
        {
            StatusCodes.Status404NotFound => NoSuchEndpoint,
            StatusCodes.Status405MethodNotAllowed => MethodNotAllowed,
            _ => Unhandled
        };

        return CustomResults.WriteProblemAsync(context, error);
    }
}
