using Domain.Shared;

namespace Api.Infrastructure;

/// <summary>
/// The single place an <see cref="Error"/> becomes an HTTP response.
/// </summary>
/// <remarks>
/// No endpoint and no middleware builds a problem document by hand, so a
/// rejection from <c>CsrfMiddleware</c> has exactly the shape a rejection from
/// a handler has and the frontend has one response format to parse.
/// </remarks>
internal static class CustomResults
{
    /// <summary>Renders a failure as an RFC 9457 problem document.</summary>
    /// <param name="error">The failure to report.</param>
    internal static IResult Problem(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new ProblemResult(error);
    }

    /// <summary>
    /// Writes a problem document directly to a response, for middleware that
    /// rejects a request before routing has chosen an endpoint.
    /// </summary>
    /// <param name="context">The request being rejected.</param>
    /// <param name="error">The failure to report.</param>
    /// <param name="statusOverride">
    /// Keeps a status the framework already chose, instead of deriving one
    /// from the error type.
    /// </param>
    internal static Task WriteProblemAsync(HttpContext context, Error error, int? statusOverride = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(error);

        return new ProblemResult(error, statusOverride).ExecuteAsync(context);
    }
}
