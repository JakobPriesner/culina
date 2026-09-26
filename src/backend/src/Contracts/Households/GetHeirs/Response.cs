namespace Contracts.Households.GetHeirs;

/// <summary>The households that see this one's recipes.</summary>
public sealed record Response
{
    /// <summary>Those inheriting from it first, then those inheriting from them.</summary>
    public required IReadOnlyList<Heir> Items { get; init; }
}

/// <summary>A household that inherits this one's recipes, directly or not.</summary>
public sealed record Heir
{
    /// <summary>Which household.</summary>
    public required Guid HouseholdId { get; init; }

    /// <summary>What it is called.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The household it inherits from directly. The one asked about for a
    /// direct heir — the only kind its owners can cut loose — or the heir it
    /// inherits through.
    /// </summary>
    public required Guid InheritsFrom { get; init; }
}
