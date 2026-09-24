namespace Application.Abstractions;

/// <summary>How far a fresh instance has got in being set up.</summary>
public enum SetupStage
{
    /// <summary>There is no database to use yet.</summary>
    Database,

    /// <summary>There is a database, and nobody has an account in it.</summary>
    Account,

    /// <summary>Somebody administers this instance.</summary>
    Complete,
}

/// <summary>
/// Answers how far this instance has got in being set up.
/// </summary>
/// <remarks>
/// A port because the answer comes from two different places: a host with no
/// database knows it is at the first step without asking anything, and a host
/// with one has to count accounts.
/// </remarks>
public interface ISetupProgress
{
    /// <summary>The current stage.</summary>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    Task<SetupStage> CurrentAsync(CancellationToken cancellationToken);
}
