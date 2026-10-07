using System.Diagnostics;
using Application.Telemetry;
using Infrastructure.Persistence;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.Logging;

namespace IntegrationTests.Persistence;

/// <summary>
/// The early warning for a pool running dry, which must never become the
/// thing that floods the log.
/// </summary>
public sealed class ConnectionPoolWatchTests : IDisposable
{
    private const int SlowConnection = 1970;
    private const int ConnectionHeldLong = 1971;

    private readonly ManualTime time = new();
    private readonly RecordingLogs logs = new();
    private readonly ILoggerFactory loggers;
    private readonly ConnectionPoolWatch watch;

    public ConnectionPoolWatchTests()
    {
        loggers = LoggerFactory.Create(builder => builder.AddProvider(logs));
        watch = new ConnectionPoolWatch(time, loggers.CreateLogger<ConnectionPoolWatch>());
    }

    [Fact]
    public void Acquired_ShouldWarn_WhenGettingAConnectionTookLong()
    {
        Acquire(waiting: TimeSpan.FromMilliseconds(800));

        var line = Assert.Single(logs.Lines);
        Assert.Equal(SlowConnection, line.EventId);
        Assert.Equal(LogLevel.Warning, line.Level);
        Assert.Equal("800", line["WaitMilliseconds"]);
    }

    [Fact]
    public void Watch_ShouldStayQuiet_WhileThePoolKeepsUp()
    {
        var acquiredAt = Acquire(waiting: TimeSpan.FromMilliseconds(3));
        Return(acquiredAt, holding: TimeSpan.FromMilliseconds(40));

        Assert.Empty(logs.Lines);
    }

    [Fact]
    public void Acquired_ShouldWarnOnceAMinute_AndCountTheWaitsInBetween()
    {
        // A pool that has run dry makes every request wait; one line each
        // would bury the log in the very moment somebody reads it.
        Acquire(waiting: TimeSpan.FromSeconds(1));
        Acquire(waiting: TimeSpan.FromSeconds(1));
        Acquire(waiting: TimeSpan.FromSeconds(1));

        time.Advance(ConnectionPoolWatch.ReportEvery);
        Acquire(waiting: TimeSpan.FromSeconds(1));

        Assert.Collection(
            logs.Lines,
            first => Assert.Equal("0", first["Unreported"]),
            second => Assert.Equal("2", second["Unreported"]));
    }

    [Fact]
    public void Returned_ShouldNameTheUseCase_WhenAConnectionWasHeldLong()
    {
        using var listener = ListenToCulina();
        using var useCase = CulinaTelemetry.ActivitySource.StartActivity("Recipes.Import");
        var acquiredAt = Acquire(waiting: TimeSpan.Zero);

        Return(acquiredAt, holding: TimeSpan.FromSeconds(30));

        // The use case points at the code that kept the connection; the request
        // or the SQL would only point at where it happened to be noticed.
        var line = Assert.Single(logs.Lines);
        Assert.Equal(ConnectionHeldLong, line.EventId);
        Assert.Equal("30000", line["HeldMilliseconds"]);
        Assert.Equal("Recipes.Import", line["UseCase"]);
    }

    [Fact]
    public void Throttle_ShouldKeepTheTwoWarningsApart()
    {
        var acquiredAt = Acquire(waiting: TimeSpan.FromSeconds(1));

        Return(acquiredAt, holding: TimeSpan.FromSeconds(10));

        // A slow wait just reported says nothing about why: the hold that
        // caused it is reported in its own right.
        Assert.Equal([SlowConnection, ConnectionHeldLong], logs.Lines.Select(line => line.EventId));
    }

    public void Dispose()
    {
        loggers.Dispose();
        logs.Dispose();
    }

    private long Acquire(TimeSpan waiting)
    {
        var requestedAt = watch.Timestamp();
        time.Advance(waiting);

        return watch.Acquired(requestedAt);
    }

    private void Return(long acquiredAt, TimeSpan holding)
    {
        time.Advance(holding);
        watch.Returned(acquiredAt);
    }

    private static ActivityListener ListenToCulina()
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == CulinaTelemetry.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };

        ActivitySource.AddActivityListener(listener);

        return listener;
    }

    /// <summary>A clock that moves only when told to.</summary>
    private sealed class ManualTime : TimeProvider
    {
        private long now;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => now;

        internal void Advance(TimeSpan by) => now += by.Ticks;
    }
}
