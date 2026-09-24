using Application.Abstractions;

namespace TestSupport;

/// <summary>
/// Records that a restart was asked for, and does not restart.
/// </summary>
/// <remarks>
/// The real one stops the host so <c>Program</c> can build the next, which in a
/// test would stop the host every later test in the class is talking to. What
/// a test can prove is that saving asked for one; that the loop in
/// <c>Program</c> then builds the next host is exercised by running the app.
/// </remarks>
public sealed class RecordingRestart : IHostRestart
{
    private int scheduled;

    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

    /// <summary>How many restarts were asked for.</summary>
    public int Scheduled => scheduled;

    public void Schedule() => Interlocked.Increment(ref scheduled);
}
