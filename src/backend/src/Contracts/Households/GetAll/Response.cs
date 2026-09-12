using Contracts.Households;

namespace Contracts.Households.GetAll;

/// <summary>The households the caller belongs to.</summary>
public sealed record Response
{
    /// <summary>One entry per household, by name.</summary>
    public required IReadOnlyList<HouseholdSummary> Items { get; init; }
}
