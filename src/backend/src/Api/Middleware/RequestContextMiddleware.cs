using System.Diagnostics;
using Api.Infrastructure;

namespace Api.Middleware;

/// <summary>
/// Gives every request a correlation id, and puts the client address on the
/// logging scope.
/// </summary>
/// <remarks>
/// <para>
/// Runs second in the pipeline, right after forwarded headers, so every line
/// emitted afterwards carries the same id. The id is also set as a response
/// header and written into every problem document, which is what lets a user
/// paste it from an error message and an operator find the whole trace.
/// </para>
/// <para>
/// The id is the current trace id when OpenTelemetry has started an activity,
/// so a log line and a span can be joined without a second identifier. It
/// reaches the log as <c>TraceId</c>, from the activity tracking the logging
/// setup switches on, and is not pushed again here: a second copy would only
/// repeat it, and the name <c>RequestId</c> is the host's own, for its
/// connection-based identifier.
/// </para>
/// <para>
/// The client address is the one forwarded headers settled on. It is what a
/// refused sign-in has to name for fail2ban or CrowdSec to act on it, and what
/// an operator compares when one client misbehaves.
/// </para>
/// </remarks>
/// <param name="next">The rest of the pipeline.</param>
internal sealed class RequestContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ILogger<RequestContextMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        var requestId = Activity.Current?.TraceId.ToString() ?? Guid.CreateVersion7().ToString("n");

        RequestContext.SetRequestId(context, requestId);
        context.Response.Headers[CulinaHeaders.RequestId] = requestId;

        using var scope = logger.BeginScope(new LogScope(
            new KeyValuePair<string, object?>(
                "ClientAddress",
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown")));

        await next(context).ConfigureAwait(false);
    }
}
