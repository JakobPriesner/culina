namespace Contracts.Setup.Get;

/// <summary>How far this instance has got in being set up.</summary>
public sealed record Response
{
    /// <summary>
    /// <c>database</c> when there is none yet, <c>account</c> when nobody has an account,
    /// <c>complete</c> once somebody administers it.
    /// </summary>
    public required string Stage { get; init; }

    /// <summary>
    /// When the running host started; it changes when the server restarts to apply a setting, so a
    /// client can tell the restart is over.
    /// </summary>
    public required DateTimeOffset StartedAt { get; init; }
}
