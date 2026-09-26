namespace Contracts.Recipes.Copy;

/// <summary>Where the copy goes.</summary>
public sealed record Request
{
    /// <summary>A household you are in. The copy is its own, to change as it likes.</summary>
    public required Guid HouseholdId { get; init; }
}
