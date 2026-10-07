using Domain.Shared;

namespace Application.Abstractions.Settings;

/// <summary>Tries a set of connection details before anything is saved.</summary>
public interface IDatabaseConnectionCheck
{
    /// <summary>Connects and confirms Culina could run there (extensions exist, tables can be created).</summary>
    /// <param name="settings">The details to try.</param>
    /// <param name="cancellationToken">Cancels the attempt.</param>
    Task<Result> CheckAsync(DatabaseSettings settings, CancellationToken cancellationToken);
}
