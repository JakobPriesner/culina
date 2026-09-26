namespace Contracts.Households.SetInheritance;

/// <summary>Which household to inherit recipes from.</summary>
public sealed record Request
{
    /// <summary>
    /// The household whose recipes this one should see, or null to inherit
    /// nothing. Required either way, so a missing field is never mistaken for
    /// "stop inheriting".
    /// </summary>
    public required Guid? HouseholdId { get; init; }
}
