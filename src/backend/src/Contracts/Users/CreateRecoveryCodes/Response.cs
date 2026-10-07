namespace Contracts.Users.CreateRecoveryCodes;

/// <summary>A new set of recovery codes. Any earlier set has stopped working.</summary>
public sealed record Response
{
    /// <summary>
    /// The codes, each good for one password reset; shown exactly once, as only their digests are
    /// stored.
    /// </summary>
    public required IReadOnlyList<string> Codes { get; init; }

    /// <summary>When the set was made.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
