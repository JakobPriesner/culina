namespace Application.Abstractions;

/// <summary>When this host started, and the way to start it again.</summary>
/// <remarks>
/// Bootstrap settings are read once, so a saved change applies when the host is built again: in the
/// same process (close listener, build from fresh configuration, listen again), not by exiting and
/// hoping something restarts the container.
/// </remarks>
public interface IHostRestart
{
    /// <summary>
    /// When this host was built; a client that asked for a restart waits for a different value.
    /// </summary>
    DateTimeOffset StartedAt { get; }

    /// <summary>
    /// Stops this host once in-flight requests finish, and builds the next.
    /// </summary>
    void Schedule();
}
