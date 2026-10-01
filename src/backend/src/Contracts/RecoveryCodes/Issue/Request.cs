namespace Contracts.RecoveryCodes.Issue;

/// <summary>Whose account the administrator is helping back in.</summary>
public sealed record Request
{
    /// <summary>The address the account signs in with.</summary>
    public required string Email { get; init; }
}
