using System.Diagnostics;
using Api.Authentication;
using Microsoft.Extensions.Primitives;

namespace Api.Middleware;

/// <summary>
/// Puts the authenticated user on the logging scope and the current span.
/// </summary>
/// <remarks>
/// Position 12: after authentication has produced a principal and before
/// anything that needs to know who is calling. Without it, every log line from
/// a signed-in request would be correlated by request id but anonymous, and
/// "what was this user doing" would be unanswerable from the telemetry.
/// </remarks>
/// <param name="next">The rest of the pipeline.</param>
internal sealed class SessionContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ILogger<SessionContextMiddleware> logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);

        if (context.User.FindFirst(CulinaClaims.UserId)?.Value is not { Length: > 0 } userId)
        {
            await next(context).ConfigureAwait(false);

            return;
        }

        // A user id is an acceptable span tag: it is low cardinality relative
        // to an instance's traffic and it is what an operator searches by. An
        // email or a search query would not be.
        Activity.Current?.SetTag("culina.user_id", userId);

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["UserId"] = userId
        });

        await next(context).ConfigureAwait(false);
    }
}
