namespace Contracts.Recipes.Create;

/// <summary>
/// A new recipe; only the household and title are required, so a bare recipe can be a placeholder
/// to write up later.
/// </summary>
public sealed record Request
{
    /// <summary>Which household will own it.</summary>
    public required Guid HouseholdId { get; init; }

    /// <summary>What to call it.</summary>
    public required string Title { get; init; }

    /// <summary>
    /// The assistant draft this recipe is made from, recorded as provenance beside an imported
    /// recipe's source; omitted by every other caller.
    /// </summary>
    public Guid? DraftId { get; init; }

    /// <summary>The public page the recipe was imported from.</summary>
    public string? SourceUrl { get; init; }
}
