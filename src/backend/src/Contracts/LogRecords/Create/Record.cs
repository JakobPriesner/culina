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

    /// <summary>When it happened, by the device's clock.</summary>
    public DateTimeOffset? OccurredAt { get; init; }

    /// <summary>How long the page had been open when it happened, in milliseconds.</summary>
    public long? PageAge { get; init; }

    /// <summary>Whether the browser thought it was online when it happened.</summary>
    public bool? Online { get; init; }

    /// <summary>Whether the page was on screen when it happened.</summary>
    public bool? Visible { get; init; }

    /// <summary>The page's JavaScript heap in use when it happened, in bytes, where the browser says.</summary>
    public long? HeapUsed { get; init; }

    /// <summary>The most the page's JavaScript heap may grow to, in bytes, where the browser says.</summary>
    public long? HeapLimit { get; init; }
}
