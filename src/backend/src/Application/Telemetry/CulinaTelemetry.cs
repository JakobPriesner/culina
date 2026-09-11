using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Application.Telemetry;

/// <summary>
/// The one activity source and meter the application emits through.
/// </summary>
/// <remarks>
/// Automatic instrumentation already covers ASP.NET Core, HttpClient, Npgsql
/// and the runtime. What it cannot see is the use case, which is what these
/// exist for.
/// </remarks>
public static class CulinaTelemetry
{
    /// <summary>The activity source and meter name the collector filters on.</summary>
    public const string Name = "Culina";

    /// <summary>The one source use-case spans are started on.</summary>
    public static readonly ActivitySource ActivitySource = new(Name);

    /// <summary>The one meter application counters live on.</summary>
    public static readonly Meter Meter = new(Name);

    /// <summary>
    /// How long each use case takes, tagged with its name and outcome. Answers
    /// "what got slow" without searching traces.
    /// </summary>
    public static readonly Histogram<double> UseCaseDuration = Meter.CreateHistogram<double>(
        "culina.usecase.duration",
        unit: "ms",
        description: "Duration of a command or query handler.");

    /// <summary>Failed sign-in attempts. Worth alerting on.</summary>
    public static readonly Counter<long> LoginFailures = Meter.CreateCounter<long>(
        "culina.auth.login_failures",
        description: "Sign-in attempts rejected as invalid credentials.");

    /// <summary>Requests rejected by the CSRF check.</summary>
    public static readonly Counter<long> CsrfRejections = Meter.CreateCounter<long>(
        "culina.csrf.rejections",
        description: "Unsafe requests rejected for a missing or invalid CSRF token.");

    /// <summary>Requests rejected by a rate limiter.</summary>
    public static readonly Counter<long> RateLimitRejections = Meter.CreateCounter<long>(
        "culina.ratelimit.rejections",
        description: "Requests rejected by a rate limit policy.");

    /// <summary>Unsafe requests rejected for coming from a foreign origin.</summary>
    public static readonly Counter<long> ForeignOriginRejections = Meter.CreateCounter<long>(
        "culina.origin.rejections",
        description: "Unsafe cookie-authenticated requests from an origin that is not ours.");
}
