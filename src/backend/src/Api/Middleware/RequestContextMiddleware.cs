using System.Diagnostics;
using Api.Infrastructure;

namespace Api.Middleware;

/// <summary>
/// Gives every request a correlation id and puts it on the logging scope.
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
/// so a log line and a span can be joined without a second identifier.
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

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestId"] = requestId
        });

        await next(context).ConfigureAwait(false);
    }
}
