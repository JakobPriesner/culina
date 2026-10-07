using System.Diagnostics;
using Api.Authentication;
using Api.Infrastructure;
using Microsoft.Extensions.Primitives;

namespace Api.Middleware;

/// <summary>
/// Puts the authenticated user on the logging scope and the current span (position 12: after authentication),
/// so signed-in log lines are attributable.
/// </summary>
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

        // A user id is low cardinality and what an operator searches by; an email or search query would not be acceptable.
        Activity.Current?.SetTag("culina.user_id", userId);

        using var scope = logger.BeginScope(new LogScope(new KeyValuePair<string, object?>("UserId", userId)));

        await next(context).ConfigureAwait(false);
    }
}
