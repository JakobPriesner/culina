namespace Contracts.RecoveryCodes.Issue;

/// <summary>A one-time code to pass on to the person who is locked out.</summary>
public sealed record Response
{
    /// <summary>
    /// The code. Shown exactly once — only its digest is stored, so it cannot
    /// be shown again.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>When it stops working.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}
