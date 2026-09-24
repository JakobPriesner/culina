using Domain.Shared;

namespace Application.Abstractions.Settings;

/// <summary>
/// Tries a set of connection details before anything is saved.
/// </summary>
/// <remarks>
/// A port because the question is about PostgreSQL, and Application must not
/// know what PostgreSQL is. It is asked before saving because the answer after
/// saving is a process that fails its migrations on every start.
/// </remarks>
public interface IDatabaseConnectionCheck
{
    /// <summary>
    /// Connects, and confirms Culina could run there: the extensions it needs
    /// exist and it may create tables.
    /// </summary>
    /// <param name="settings">The details to try.</param>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    Task<Result> CheckAsync(DatabaseSettings settings, CancellationToken cancellationToken);
}
