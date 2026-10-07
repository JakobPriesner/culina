using System.Diagnostics;
using Api.Infrastructure;

namespace Api.Middleware;

/// <summary>
/// Gives every request a correlation id, and puts the client address on the logging scope.
/// </summary>
/// <remarks>
/// Runs right after forwarded headers so every later line carries the id, which is also a response
/// header and in every problem document. The id is the trace id when an activity exists; it reaches
/// the log as <c>TraceId</c> (<c>RequestId</c> is the host's own). The client address is what
/// fail2ban or CrowdSec act on.
/// </remarks>
internal sealed class RequestContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ILogger<RequestContextMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        var requestId = Activity.Current?.TraceId.ToString() ?? Guid.CreateVersion7().ToString("n");

        RequestContext.SetRequestId(context, requestId);

        // Set as the response starts: the exception handler clears every header, and a 500 most
        // needs its id.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CulinaHeaders.RequestId] = requestId;

            return Task.CompletedTask;
        });

        using var scope = logger.BeginScope(new LogScope(
            new KeyValuePair<string, object?>(
                "ClientAddress",
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown")));

        await next(context).ConfigureAwait(false);
    }
}
