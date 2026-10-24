namespace Contracts.Users.UpdateDiaryEntry;

/// <summary>The entry after the change.</summary>
public sealed record Response
{
    /// <summary>The changed entry.</summary>
    public required DiaryEntryDetail Entry { get; init; }
}
