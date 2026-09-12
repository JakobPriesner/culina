using Contracts.Recipes.GetNotes;

namespace Contracts.Recipes.SaveNotes;

/// <summary>
/// Your notes on one recipe, in full.
/// </summary>
/// <remarks>
/// A replacement: an omitted step note is deleted, and an empty body means the
/// same thing. Storing a blank note would make the UI show an empty box nobody
/// asked for.
/// </remarks>
public sealed record Request
{
    /// <summary>A note about the recipe as a whole, or null to remove it.</summary>
    public string? Overall { get; init; }

    /// <summary>Notes attached to individual steps.</summary>
    public required IReadOnlyList<StepNote> Steps { get; init; }
}
