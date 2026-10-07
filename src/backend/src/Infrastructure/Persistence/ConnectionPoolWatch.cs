using System.Diagnostics;
using Application.Telemetry;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

/// <summary>Warns when the connection pool is getting tight, before requests fail for want of a connection.</summary>
/// <remarks>
/// Two signs: a slow acquire (no idle connection) and a long hold (a transaction open across slow work).
/// Npgsql's metrics measure neither. Each is logged at most once a minute with a count of the skipped ones.
/// </remarks>
internal sealed class ConnectionPoolWatch(TimeProvider time, ILogger<ConnectionPoolWatch> logger)
{
    // Pool opens take microseconds and new connections milliseconds; half a second means nothing idle.
    internal static readonly TimeSpan SlowWait = TimeSpan.FromMilliseconds(500);

    internal static readonly TimeSpan LongHold = TimeSpan.FromSeconds(5);

    internal static readonly TimeSpan ReportEvery = TimeSpan.FromMinutes(1);

    private readonly Throttle slowWaits = new(time);
    private readonly Throttle longHolds = new(time);

    internal long Timestamp() => time.GetTimestamp();

    /// <summary>Notes how long getting a connection took; returns when it was got, for <see cref="Returned"/>.</summary>
    internal long Acquired(long requestedAt)
    {
        var acquiredAt = time.GetTimestamp();
        var waited = time.GetElapsedTime(requestedAt, acquiredAt);

        if (waited >= SlowWait && slowWaits.ShouldReport(acquiredAt, out var unreported))
        {
            logger.SlowConnection((long)waited.TotalMilliseconds, unreported);
        }

        return acquiredAt;
    }

    /// <summary>Notes how long a connection was kept out of the pool.</summary>
    internal void Returned(long acquiredAt)
    {
        var returnedAt = time.GetTimestamp();
        var held = time.GetElapsedTime(acquiredAt, returnedAt);

        if (held >= LongHold && longHolds.ShouldReport(returnedAt, out var unreported))
        {
            logger.ConnectionHeldLong((long)held.TotalMilliseconds, UseCase(), unreported);
        }
    }

    // The use case's span name, which points at the code rather than the request or SQL.
    private static string UseCase()
    {
        for (var activity = Activity.Current; activity is not null; activity = activity.Parent)
        {
            if (activity.Source.Name == CulinaTelemetry.Name)
            {
                return activity.OperationName;
            }
        }

        return "no use case";
    }

    /// <summary>One report a minute, and a count of what it left out.</summary>
    private sealed class Throttle(TimeProvider time)
    {
        private readonly Lock gate = new();
        private long? reportedAt;
        private int unreported;

        internal bool ShouldReport(long now, out int skipped)
        {
            lock (gate)
            {
                if (reportedAt is { } last && time.GetElapsedTime(last, now) < ReportEvery)
                {
                    unreported++;
                    skipped = 0;

                    return false;
                }

                reportedAt = now;
                skipped = unreported;
                unreported = 0;

                return true;
            }
        }
    }
}
