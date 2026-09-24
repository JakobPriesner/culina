namespace Contracts.Households.RedeemInvitation;

/// <summary>The household the caller just joined, or was already in.</summary>
public sealed record Response
{
    /// <summary>The household's id.</summary>
    public required Guid HouseholdId { get; init; }

    /// <summary>What it is called.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// True when the caller already belonged to it, in which case the code was
    /// not used up and still works for whoever it was meant for.
    /// </summary>
    public required bool AlreadyMember { get; init; }
}
