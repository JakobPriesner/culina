namespace Contracts.Recipes.Drafts;

/// <summary>Asks the assistant for a recipe. One request with a <c>kind</c> rather than three endpoints.</summary>
public sealed record Request
{
    /// <summary>What is being asked for: <c>idea</c>, <c>text</c>, <c>social</c> or <c>revision</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Whose kitchen it is for.</summary>
    public required Guid HouseholdId { get; init; }

    /// <summary>The material for <c>idea</c> and <c>text</c>: words the person supplied, never treated as an instruction.</summary>
    public string? Material { get; init; }

    /// <summary>Spoken captions, kept apart from the measured written recipe.</summary>
    public string? Transcript { get; init; }

    /// <summary>Which recipe to rewrite, for <c>revision</c>.</summary>
    public Guid? RecipeId { get; init; }

    /// <summary>The language to answer in: <c>en</c> or <c>de</c>. Sent, not inferred; ignored for a revision, which keeps its language.</summary>
    public string? Language { get; init; }
}

/// <summary>A recipe the assistant wrote. Not saved: it is read beside the original and accepted field by field.</summary>
public sealed record Response
{
    /// <summary>This draft's own id, sent back when the draft becomes a recipe so provenance can be recorded.</summary>
    public required Guid DraftId { get; init; }

    /// <summary>What the assistant called it.</summary>
    public string? Title { get; init; }

    /// <summary>A sentence or two about it.</summary>
    public string? Description { get; init; }

    /// <summary>How many it makes.</summary>
    public decimal? YieldAmount { get; init; }

    /// <summary>What it makes: servings, or a cake.</summary>
    public string? YieldLabel { get; init; }

    /// <summary>Minutes of hands-on work.</summary>
    public int? PrepMinutes { get; init; }

    /// <summary>Minutes of cooking.</summary>
    public int? CookMinutes { get; init; }

    /// <summary>The ingredient groups, in order.</summary>
    public required IReadOnlyList<DraftGroupContract> Groups { get; init; }

    /// <summary>The steps, in order.</summary>
    public required IReadOnlyList<DraftStepContract> Steps { get; init; }

    /// <summary>What to file it under.</summary>
    public required IReadOnlyList<string> Tags { get; init; }
}

/// <summary>A heading and the lines under it.</summary>
public sealed record DraftGroupContract
{
    /// <summary>The heading, or null for the implicit first group.</summary>
    public string? Name { get; init; }

    /// <summary>Its lines, in order.</summary>
    public required IReadOnlyList<DraftIngredientContract> Ingredients { get; init; }
}

/// <summary>One ingredient line.</summary>
public sealed record DraftIngredientContract
{
    /// <summary>How much, or null when the recipe does not say.</summary>
    public decimal? Quantity { get; init; }

    /// <summary>In what, or null. Already checked against what the app can store; impossible units are dropped.</summary>
    public string? Unit { get; init; }

    /// <summary>The shoppable noun.</summary>
    public required string Name { get; init; }

    /// <summary>The preparation.</summary>
    public string? Note { get; init; }
}

/// <summary>One instruction.</summary>
/// <remarks>Plain text, not segments: the client already marks up ingredient mentions.</remarks>
public sealed record DraftStepContract
{
    /// <summary>What this step is called, when it is called anything.</summary>
    public string? Title { get; init; }

    /// <summary>What to do.</summary>
    public required string Text { get; init; }

    /// <summary>How long it waits, when it waits.</summary>
    public int? DurationSeconds { get; init; }
}

/// <summary>One moment of a recipe being written.</summary>
/// <remarks>
/// The whole draft every time, not a delta, so the client never holds a second idea of what the draft says.
/// Every event of one ask carries the same <c>draftId</c>.
/// </remarks>
public sealed record Event
{
    /// <summary>The recipe as far as it has been written. An unfinished field is absent, never half-written.</summary>
    public required Response Draft { get; init; }

    /// <summary>Whether this is the last one. Explicit because a proxy dropping the connection also closes the stream.</summary>
    public bool Finished { get; init; }

    /// <summary>Why it stopped, when the last event is a failure. The draft beside it is what was written before.</summary>
    public Streaming.Problem? Problem { get; init; }
}
