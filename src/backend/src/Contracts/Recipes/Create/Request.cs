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

    /// <summary>
    /// The assistant draft this recipe is being made from, when it is.
    /// </summary>
    /// <remarks>
    /// Omitted by every other caller, and the only way a recipe comes to know
    /// it was drafted rather than typed. Recorded as provenance in exactly the
    /// same place an imported recipe records where it came from — which is why
    /// it is a field here rather than an endpoint of its own.
    /// </remarks>
    public Guid? DraftId { get; init; }
}
