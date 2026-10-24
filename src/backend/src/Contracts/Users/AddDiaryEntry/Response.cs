namespace Contracts.Users.AddDiaryEntry;

/// <summary>The entry as it was logged, with the figures the server looked up.</summary>
public sealed record Response
{
    /// <summary>The new entry.</summary>
    public required DiaryEntryDetail Entry { get; init; }
}
