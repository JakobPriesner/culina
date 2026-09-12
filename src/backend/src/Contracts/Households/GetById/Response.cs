using Contracts.Households;

namespace Contracts.Households.GetById;

/// <summary>One household, with everyone in it.</summary>
public sealed record Response : HouseholdSummary
{
    /// <summary>When it was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Its members.</summary>
    public required IReadOnlyList<Member> Members { get; init; }
}
