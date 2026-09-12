namespace Contracts.Households;

/// <summary>
/// A household as it appears in a list.
/// </summary>
/// <remarks>
/// Shared by inheritance rather than by reuse: an operation that needs an extra
/// field derives from this instead of adding one here, so a change for one
/// response cannot alter another.
/// </remarks>
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
}
