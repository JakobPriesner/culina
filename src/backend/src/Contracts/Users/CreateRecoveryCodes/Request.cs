namespace Contracts.Users.CreateRecoveryCodes;

/// <summary>The password, because recovery codes outlive any session.</summary>
public sealed record Request
{
    /// <summary>The password the account has now.</summary>
    public required string Password { get; init; }
}
