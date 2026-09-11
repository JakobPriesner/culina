using Domain.Shared;
using Microsoft.AspNetCore.Diagnostics;

namespace Api.Infrastructure;

/// <summary>
/// Turns any unhandled exception into a problem document.
/// </summary>
/// <remarks>
/// <para>
/// This is the only place an unhandled exception is logged. Nothing else
/// catches, logs and rethrows: that produces the same stack twice and hides the
/// site that actually threw.
/// </para>
/// <para>
/// The exception's message never reaches the client. It can contain a
/// connection string, a file path or a SQL fragment, and the caller has the
/// request id, which is what an operator needs to find the real detail.
/// </para>
/// </remarks>
/// <param name="logger">Records the defect, exactly once.</param>
internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    private static readonly Error Unexpected = new(
        "server.unexpected",
        "Something went wrong on our side. Try again, and quote the request id if it keeps happening.",
        ErrorType.Failure);

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        logger.Unhandled(httpContext.Request.Method, httpContext.Request.Path, exception);

        await CustomResults.WriteProblemAsync(httpContext, Unexpected).ConfigureAwait(false);

        return true;
    }
}
