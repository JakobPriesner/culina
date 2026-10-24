namespace Contracts.PasswordResetLinks.Create;

/// <summary>Where to send the link.</summary>
public sealed record Request
{
    /// <summary>The address the account signs in with.</summary>
    public required string Email { get; init; }
}
