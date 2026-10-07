namespace Contracts.Households;

/// <summary>A household as it appears in a list. Extended by inheritance, so a field added for one response cannot alter another.</summary>
public record HouseholdSummary
{
    /// <summary>The household's id.</summary>
    public required Guid HouseholdId { get; init; }

    /// <summary>What it is called.</summary>
    public required string Name { get; init; }

    /// <summary>How many people are in it.</summary>
    public required int MemberCount { get; init; }

    /// <summary>What the caller may do in it.</summary>
    public required string YourRole { get; init; }

    /// <summary>The entity version, for If-Match on an update.</summary>
    public required long Version { get; init; }

    /// <summary>When it was deleted. Null for every household that is not in the bin.</summary>
    public DateTimeOffset? DeletedAt { get; init; }

    /// <summary>When a deleted household will be removed for good, unless it is restored first.</summary>
    public DateTimeOffset? PurgeAfter { get; init; }
}
