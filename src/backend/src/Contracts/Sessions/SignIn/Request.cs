namespace Contracts.Sessions.SignIn;

/// <summary>Credentials.</summary>
public sealed record Request
{
    /// <summary>The address the account was registered with.</summary>
    public required string Email { get; init; }

    /// <summary>The account's password.</summary>
    public required string Password { get; init; }
}
