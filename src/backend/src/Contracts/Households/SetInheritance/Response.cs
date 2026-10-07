using Contracts.Households;

namespace Contracts.Households.SetInheritance;

/// <summary>The household after the change.</summary>
public sealed record Response : HouseholdSummary
{
    /// <summary>
    /// Whose recipes it now sees, nearest first (the household it inherits from, then that one's
    /// parent, and so on); empty when none.
    /// </summary>
    public required IReadOnlyList<InheritedHousehold> InheritsFrom { get; init; }
}
