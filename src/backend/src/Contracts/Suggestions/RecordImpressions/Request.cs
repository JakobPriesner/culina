namespace Contracts.Suggestions.RecordImpressions;

/// <summary>The recipes a suggestion set actually showed.</summary>
public sealed record Request
{
    /// <summary>Whose kitchen the set was for.</summary>
    public required Guid HouseholdId { get; init; }

    /// <summary>The recipes that were on screen, at most as many as one set can hold.</summary>
    public required IReadOnlyList<Guid> RecipeIds { get; init; }
}
