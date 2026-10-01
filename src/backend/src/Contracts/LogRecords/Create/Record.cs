namespace Contracts.LogRecords.Create;

/// <summary>One thing that went wrong in the browser.</summary>
public sealed record Record
{
    /// <summary>What kind of thing; one of <see cref="LogRecordVocabulary.Events"/>.</summary>
    public required string Event { get; init; }

    /// <summary>What it said, at most 1,000 characters.</summary>
    public required string Message { get; init; }

    /// <summary>Where it was thrown, when there is a stack. At most 8,000 characters.</summary>
    public string? Stack { get; init; }

    /// <summary>
    /// The route it happened on, as the router names it — <c>/recipes/[recipeId]</c>,
    /// never the address with the id in it. At most 200 characters.
    /// </summary>
    public string? Route { get; init; }
}
