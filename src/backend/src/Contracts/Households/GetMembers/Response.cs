using Contracts.Households;

namespace Contracts.Households.GetMembers;

/// <summary>Everyone in a household.</summary>
public sealed record Response
{
    /// <summary>One entry per member.</summary>
    public required IReadOnlyList<Member> Items { get; init; }
}
