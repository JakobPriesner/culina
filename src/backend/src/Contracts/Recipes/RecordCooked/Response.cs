namespace Contracts.Recipes.RecordCooked;

/// <summary>The entry that was recorded.</summary>
public sealed record Response
{
    /// <summary>The entry's id, so the undo in the toast can remove it.</summary>
    public required Guid EntryId { get; init; }

    /// <summary>When it was recorded as made.</summary>
    public required DateTimeOffset MadeAt { get; init; }

    /// <summary>How many times you have now made it.</summary>
    public required int Count { get; init; }
}
