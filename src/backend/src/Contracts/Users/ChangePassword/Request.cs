namespace Contracts.Users.ChangePassword;

/// <summary>A new password, and the current one to prove it is you.</summary>
public sealed record Request
{
    /// <summary>The password the account has now.</summary>
    public required string CurrentPassword { get; init; }

    /// <summary>What it becomes. At least 12 characters.</summary>
    public required string NewPassword { get; init; }
}
