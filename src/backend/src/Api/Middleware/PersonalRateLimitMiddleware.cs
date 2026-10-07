using System.Threading.RateLimiting;
using Api.Extensions;

namespace Api.Middleware;

/// <summary>
/// Counts what costs money or makes the server work hard against the person asking for it.
/// </summary>
/// <remarks>
/// After authorization, unlike the limiter before authentication, which cannot know who asks
/// (signing in again bought a fresh budget): counted against the user id, whichever session or
/// address. Which endpoints is <c>RateLimitExtensions.PerPerson</c>; a refusal looks like the
/// limiter middleware's.
/// </remarks>
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
