using Application.Abstractions;

namespace TestSupport;

/// <summary>Records that a restart was asked for, and does not restart (the real one would stop the host the test is using).</summary>
public sealed class RecordingRestart : IHostRestart
{
    private int scheduled;

    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

    public int Scheduled => scheduled;

    public void Schedule() => Interlocked.Increment(ref scheduled);
}
