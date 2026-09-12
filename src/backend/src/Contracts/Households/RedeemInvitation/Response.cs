namespace Contracts.Households.RedeemInvitation;

/// <summary>The household the caller just joined.</summary>
public sealed record Response
{
    /// <summary>The household's id.</summary>
    public required Guid HouseholdId { get; init; }

    /// <summary>What it is called.</summary>
    public required string Name { get; init; }
}
