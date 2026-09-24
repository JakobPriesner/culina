using Application.Abstractions;

namespace Api.Infrastructure;

/// <summary>
/// Stops the running host so <c>Program</c> builds the next one.
/// </summary>
/// <remarks>
/// Stopping is graceful: the listener closes, requests already in flight —
/// including the one that asked for this — finish, and only then does the
/// host shut down. <c>Program</c> reads <see cref="Requested"/> once the host
/// has stopped, to tell a restart from a shutdown.
/// </remarks>
/// <param name="lifetime">Stops this host.</param>
/// <param name="time">Stamps when it started.</param>
/// <param name="logger">Says why the server went away for a moment.</param>
internal sealed class HostRestart(
    IHostApplicationLifetime lifetime,
    TimeProvider time,
    ILogger<HostRestart> logger) : IHostRestart
{
    public DateTimeOffset StartedAt { get; } = time.GetUtcNow();

    /// <summary>Whether the host stopped to be built again rather than to exit.</summary>
    internal bool Requested { get; private set; }

    public void Schedule()
    {
        logger.Restarting();

        Requested = true;
        lifetime.StopApplication();
    }
}
