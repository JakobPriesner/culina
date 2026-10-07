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

/// <summary>Answers how far this instance has got in being set up.</summary>
/// <remarks>
/// A port because the answer comes from two places: a host without a database is at the first step,
/// one with a database counts accounts.
/// </remarks>
public interface ISetupProgress
{
    /// <summary>The current stage.</summary>
    Task<SetupStage> CurrentAsync(CancellationToken cancellationToken);
}
