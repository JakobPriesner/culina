namespace Contracts.Recipes.Create;

/// <summary>
/// A new recipe.
/// </summary>
/// <remarks>
/// Only the household and the title are required. A recipe with nothing else is
/// valid and is the point of the create form: it is the placeholder for "I want
/// to write this down later".
/// </remarks>
public sealed record Request
{
    /// <summary>Which household will own it.</summary>
    public required Guid HouseholdId { get; init; }

    /// <summary>What to call it.</summary>
    public required string Title { get; init; }
}
