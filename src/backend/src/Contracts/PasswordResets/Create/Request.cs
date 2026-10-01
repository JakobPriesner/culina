namespace Contracts.PasswordResets.Create;

/// <summary>A new password for an account, unlocked by a recovery code.</summary>
public sealed record Request
{
    /// <summary>The address the account signs in with.</summary>
    public required string Email { get; init; }

    /// <summary>
    /// One of the account's saved recovery codes, or one the administrator
    /// issued. Case, spaces and dashes do not matter.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>The new password. At least 12 characters.</summary>
    public required string Password { get; init; }
}
