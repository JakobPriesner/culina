namespace Contracts.Users.GetRecoveryCodes;

/// <summary>What is left of your recovery codes. Never the codes themselves.</summary>
public sealed record Response
{
    /// <summary>How many codes are still unused. Zero when none were ever made.</summary>
    public required int Remaining { get; init; }

    /// <summary>When the set was made, or null when there is none.</summary>
    public DateTimeOffset? CreatedAt { get; init; }
}
