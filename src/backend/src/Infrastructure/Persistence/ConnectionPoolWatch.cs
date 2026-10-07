using System.Diagnostics;
using Application.Telemetry;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

/// <summary>
/// Warns when the connection pool is getting tight, before requests start
/// failing for want of a connection.
/// </summary>
/// <remarks>
/// <para>
/// Two early signs. A connection that took long to get means the pool had
/// none idle. A connection kept out of the pool for long, by a transaction
/// held open across slow work, is how a pool runs dry in the first place.
/// Npgsql's own metrics — connections in use
/// (<c>db.client.connection.count</c>), requests waiting
/// (<c>db.client.connection.npgsql.pending_requests</c>) and timeouts — are
/// exported with the rest, but measure neither the wait nor the hold, and an
/// instance without a collector has only its log.
/// </para>
/// <para>
/// Each sign is logged at most once a minute, with a count of the ones in
/// between, so a saturated pool cannot flood the log it is reported in.
/// </para>
/// </remarks>
/// <param name="time">Measures the waits and holds.</param>
/// <param name="logger">Where the warnings go.</param>
internal sealed class ConnectionPoolWatch(TimeProvider time, ILogger<ConnectionPoolWatch> logger)
{
    /// <summary>
    /// Opening from the pool takes microseconds and a new connection a few
    /// milliseconds; half a second is a pool with nothing idle — and still
    /// twenty times short of the connect timeout at which the wait fails.
    /// </summary>
    internal static readonly TimeSpan SlowWait = TimeSpan.FromMilliseconds(500);

    /// <summary>Longer than any statement or transaction a request should run.</summary>
    internal static readonly TimeSpan LongHold = TimeSpan.FromSeconds(5);

    internal static readonly TimeSpan ReportEvery = TimeSpan.FromMinutes(1);

    private readonly Throttle slowWaits = new(time);
    private readonly Throttle longHolds = new(time);

    internal long Timestamp() => time.GetTimestamp();

    /// <summary>Notes how long getting a connection took.</summary>
    /// <param name="requestedAt">When the connection was asked for.</param>
    /// <returns>When it was got, to hand back to <see cref="Returned"/>.</returns>
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
    /// <param name="acquiredAt">What <see cref="Acquired"/> returned for it.</param>
    internal void Returned(long acquiredAt)
    {
        var returnedAt = time.GetTimestamp();
        var held = time.GetElapsedTime(acquiredAt, returnedAt);

        if (held >= LongHold && longHolds.ShouldReport(returnedAt, out var unreported))
        {
            logger.ConnectionHeldLong((long)held.TotalMilliseconds, UseCase(), unreported);
        }
    }

    /// <summary>
    /// The use case's span, <c>&lt;Domain&gt;.&lt;Operation&gt;</c>, rather
    /// than the request's or the SQL's: it is the name that points at the code.
    /// </summary>
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
