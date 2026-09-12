namespace Contracts.Users.Register;

/// <summary>The account that was created.</summary>
public sealed record Response
{
    /// <summary>The new user's id.</summary>
    public required Guid UserId { get; init; }

    /// <summary>The address they sign in with, normalised.</summary>
    public required string Email { get; init; }

    /// <summary>What they are called.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Whether this account administers the instance.</summary>
    public required bool IsAdmin { get; init; }

    /// <summary>
    /// The household created with the account, when one was. Null means the
    /// client should offer to create one or redeem an invitation.
    /// </summary>
    public Guid? HouseholdId { get; init; }
}
