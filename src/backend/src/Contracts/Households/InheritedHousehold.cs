namespace Contracts.Households;

/// <summary>A household whose recipes another one sees.</summary>
public sealed record InheritedHousehold
{
    /// <summary>Which household.</summary>
    public required Guid HouseholdId { get; init; }

    /// <summary>What it is called.</summary>
    public required string Name { get; init; }
}
