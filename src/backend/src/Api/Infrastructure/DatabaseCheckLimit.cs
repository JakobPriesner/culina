using System.Globalization;
using System.Threading.RateLimiting;
using Application.Telemetry;
using Microsoft.AspNetCore.RateLimiting;

namespace Api.Infrastructure;

/// <summary>
/// How often one address may have the database settings tried.
/// </summary>
/// <remarks>
/// <para>
/// Trying them opens a connection to whatever host and port the request
/// names, and while nobody has an account anybody may ask — on the setup
/// host, and on the real one until the first account exists. Without a
/// ceiling that is a port scanner and a password oracle for whoever finds a
/// fresh instance first.
/// </para>
/// <para>
/// Fixed, and carried by the endpoint rather than registered with a host's
/// limiter: the setup host has no rate limit settings — they are part of what
/// is being set up — so this cannot depend on them, and it answers a refusal
/// itself for the same reason. Ten a minute is far more than anybody typing
/// connection details needs.
/// </para>
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
