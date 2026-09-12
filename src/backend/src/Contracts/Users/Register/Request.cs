namespace Contracts.Users.Register;

/// <summary>What a new account needs.</summary>
public sealed record Request
{
    /// <summary>The address they will sign in with.</summary>
    public required string Email { get; init; }

    /// <summary>What they want to be called.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Their chosen password, at least 12 characters.</summary>
    public required string Password { get; init; }

    /// <summary>
    /// What to call the household created alongside the first account on an
    /// instance. Ignored otherwise.
    /// </summary>
    public string? HouseholdName { get; init; }
}
