using System.Threading.RateLimiting;
using Api.Extensions;

namespace Api.Middleware;

/// <summary>
/// Counts what costs money or makes the server work hard against the person
/// asking for it.
/// </summary>
/// <remarks>
/// <para>
/// After authorization, unlike the limiter that runs before authentication:
/// that one cannot know who is asking, so a limit kept there could only follow
/// the session cookie, and signing in again bought a fresh budget. Here every
/// request to one of these endpoints is signed in, and it is counted against
/// the user id whichever session or address it comes from. Which endpoints,
/// and how many, is <c>RateLimitExtensions.PerPerson</c>.
/// </para>
/// <para>
/// A refusal has the shape of one from the limiter middleware: a problem
/// document, a <c>Warning</c> and a counter.
/// </para>
/// </remarks>
/// <param name="next">The rest of the pipeline.</param>
/// <param name="limiter">The per-person budgets, kept for the life of the host.</param>
internal sealed class PersonalRateLimitMiddleware(RequestDelegate next, PartitionedRateLimiter<HttpContext> limiter)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        using var lease = limiter.AttemptAcquire(context);

        if (!lease.IsAcquired)
        {
            await RateLimitExtensions.RejectAsync(context, lease).ConfigureAwait(false);

            return;
        }

        await next(context).ConfigureAwait(false);
    }
}
