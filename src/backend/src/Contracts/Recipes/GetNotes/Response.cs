namespace Contracts.Recipes.GetNotes;

/// <summary>Your notes on one recipe.</summary>
/// <remarks>
/// The recipe-level note and every step note in one document, because the notes
/// panel reads and writes them together.
/// </remarks>
public sealed record Response
{
    /// <summary>A note about the recipe as a whole, if there is one.</summary>
    public string? Overall { get; init; }

    /// <summary>Notes attached to individual steps.</summary>
    public required IReadOnlyList<StepNote> Steps { get; init; }
}

/// <summary>A note attached to one step.</summary>
public sealed record StepNote
{
    /// <summary>Which step.</summary>
    public required Guid StepId { get; init; }

    /// <summary>What it says.</summary>
    public required string Body { get; init; }
}
