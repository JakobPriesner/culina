namespace Contracts.Recipes.GetCookLog;

/// <summary>How often you have cooked this, and when.</summary>
public sealed record Response
{
    /// <summary>How many times you have made it.</summary>
    public required int Count { get; init; }

    /// <summary>When you last made it, if ever.</summary>
    public DateTimeOffset? LastMadeAt { get; init; }

    /// <summary>Every entry, newest first.</summary>
    public required IReadOnlyList<CookLogItem> Items { get; init; }
}

/// <summary>One time you cooked it.</summary>
public sealed record CookLogItem
{
    /// <summary>The entry's id, for undoing it.</summary>
    public required Guid EntryId { get; init; }

    /// <summary>When.</summary>
    public required DateTimeOffset MadeAt { get; init; }

    /// <summary>How much you made, if you said.</summary>
    public decimal? Servings { get; init; }

    /// <summary>Anything you wrote.</summary>
    public string? Note { get; init; }
}
