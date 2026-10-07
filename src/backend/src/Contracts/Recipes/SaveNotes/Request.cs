using Contracts.Recipes.GetNotes;

namespace Contracts.Recipes.SaveNotes;

/// <summary>Your notes on one recipe, in full.</summary>
/// <remarks>
/// A replacement: an omitted step note is deleted, as is an empty body; a stored blank note would
/// show an empty box nobody asked for.
/// </remarks>
public sealed record Request
{
    /// <summary>A note about the recipe as a whole, or null to remove it.</summary>
    public string? Overall { get; init; }

    /// <summary>Notes attached to individual steps.</summary>
    public required IReadOnlyList<StepNote> Steps { get; init; }
}
