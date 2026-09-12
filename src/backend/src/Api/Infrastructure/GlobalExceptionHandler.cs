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

    /// <summary>
    /// A body the server could not read.
    /// </summary>
    /// <remarks>
    /// The framework raises this before any handler runs — a missing required
    /// field, a string where a number belongs — and left alone it arrives as a
    /// 500 with a stack trace in the log. It is neither: the caller sent
    /// something the contract does not describe, and saying "our side" for it
    /// sends them looking in the wrong place while burying real faults under
    /// noise.
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
            // Logged at a level that does not page anyone: it is worth seeing
            // when a client is misbehaving, and it is not a defect here.
            // `.Value` rather than the PathString itself: the implicit conversion
            // allocates, and this line is often below the configured level.
            logger.UnreadableBody(
                httpContext.Request.Method,
                httpContext.Request.Path.Value ?? string.Empty);

            await CustomResults.WriteProblemAsync(httpContext, Unreadable).ConfigureAwait(false);

            return true;
        }

        logger.Unhandled(httpContext.Request.Method, httpContext.Request.Path, exception);

        await CustomResults.WriteProblemAsync(httpContext, Unexpected).ConfigureAwait(false);

        return true;
    }
}
