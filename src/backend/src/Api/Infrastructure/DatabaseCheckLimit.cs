using System.Globalization;
using System.Threading.RateLimiting;
using Application.Telemetry;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Infrastructure;

/// <summary>How often one address may have the database settings tried: ten a minute.</summary>
/// <remarks>
/// Trying them connects to a caller-named host, and anybody may ask until the first account exists, so without a
/// ceiling this is a port scanner and password oracle. Fixed and carried by the endpoint, because the setup host
/// has no rate limit settings.
/// </remarks>
internal sealed class DatabaseCheckLimit : IRateLimiterPolicy<string>
{
    internal const int AttemptsPerMinute = 10;

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; } = RejectAsync;

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        return RateLimitPartition.GetFixedWindowLimiter(
            $"ip:{httpContext.Connection.RemoteIpAddress}",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = AttemptsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
    }

    private static async ValueTask RejectAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var http = context.HttpContext;

        CulinaTelemetry.RateLimitRejections.Add(1);
        http.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger<DatabaseCheckLimit>()
            .DatabaseChecksLimited(AttemptsPerMinute);

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            http.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        await CustomResults.WriteProblemAsync(http, RequestErrors.RateLimited).ConfigureAwait(false);

        _ = cancellationToken;
    }
}
