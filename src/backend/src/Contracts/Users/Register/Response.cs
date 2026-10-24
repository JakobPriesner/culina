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
    /// The household created with the account; null means the client should offer to create one or
    /// redeem an invitation.
    /// </summary>
    public Guid? HouseholdId { get; init; }

    /// <summary>
    /// Whether the account must confirm its address before it can sign in; a link was mailed to it. False when the
    /// instance sends no mail, and for the first account.
    /// </summary>
    public bool EmailConfirmationRequired { get; init; }
}
