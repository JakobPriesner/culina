namespace Contracts.Setup.Get;

/// <summary>How far this instance has got in being set up.</summary>
public sealed record Response
{
    /// <summary>
    /// <c>database</c> when there is none yet, <c>account</c> when there is and
    /// nobody has an account in it, <c>complete</c> once somebody administers
    /// the instance.
    /// </summary>
    public required string Stage { get; init; }

    /// <summary>
    /// When the running host started. Changes whenever the server restarts to
    /// apply a setting, which is how a client that asked for one knows it is
    /// over.
    /// </summary>
    public required DateTimeOffset StartedAt { get; init; }
}
