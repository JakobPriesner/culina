using Application.Abstractions;

namespace Api.Infrastructure;

/// <summary>Stops the running host gracefully (in-flight requests finish) so <c>Program</c> builds the next one; <see cref="Requested"/> tells a restart from a shutdown.</summary>
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
