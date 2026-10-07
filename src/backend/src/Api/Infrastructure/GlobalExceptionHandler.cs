using Domain.Shared;
using Microsoft.AspNetCore.Diagnostics;

namespace Api.Infrastructure;

/// <summary>Turns any unhandled exception into a problem document.</summary>
/// <remarks>
/// The only place an unhandled exception is logged (catch-log-rethrow prints the stack twice). The
/// message never reaches the client, as it can hold a connection string, path or SQL; the request
/// id lets an operator find it.
/// </remarks>
internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    private static readonly Error Unexpected = new(
        "server.unexpected",
        "Something went wrong on our side. Try again, and quote the request id if it keeps happening.",
        ErrorType.Failure);

    /// <summary>A body the server could not read.</summary>
    /// <remarks>
    /// Raised by the framework before any handler (missing field, string where a number belongs):
    /// the caller broke the contract, which is not a 500 with a stack trace.
    /// </remarks>
    private static readonly Error Unreadable = new(
        "request.unreadable",
        "That request body could not be read. Check it against the API contract.",
        ErrorType.Validation);

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is BadHttpRequestException)
        {
            // Logged below paging level: worth seeing when a client misbehaves, not a defect here.
            logger.UnreadableBody(httpContext.Request);

            await CustomResults.WriteProblemAsync(httpContext, Unreadable).ConfigureAwait(false);

            return true;
        }

        logger.Unhandled(httpContext.Request, exception);

        await CustomResults.WriteProblemAsync(httpContext, Unexpected).ConfigureAwait(false);

        return true;
    }
}
