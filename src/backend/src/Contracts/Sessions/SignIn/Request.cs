namespace Contracts.Sessions.SignIn;

/// <summary>Credentials.</summary>
public sealed record Request
{
    /// <summary>The address the account was registered with.</summary>
    public required string Email { get; init; }

    /// <summary>The account's password.</summary>
    public required string Password { get; init; }

    /// <summary>
    /// <c>browser</c> (the default) for the session cookie; <c>ios</c> or <c>android</c> for an app,
    /// which receives the session token in the response and sends it as
    /// <c>Authorization: Bearer</c>.
    /// </summary>
    public string? Client { get; init; }
}
