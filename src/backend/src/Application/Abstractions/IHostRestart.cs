namespace Application.Abstractions;

/// <summary>
/// When this host started, and the way to start it again.
/// </summary>
/// <remarks>
/// Bootstrap settings are read once, into immutable records, so a saved change
/// applies when the host is built again. That happens inside the same process
/// — the listener closes, a new host is built from fresh configuration, and it
/// listens again a second or two later — rather than by exiting and hoping
/// something restarts the container.
/// </remarks>
public interface IHostRestart
{
    /// <summary>
    /// When this host was built. A client that asked for a restart waits for a
    /// different value.
    /// </summary>
    DateTimeOffset StartedAt { get; }

    /// <summary>
    /// Stops this host once in-flight requests finish, and builds the next.
    /// </summary>
    void Schedule();
}
