namespace Contracts.Users.GetDiary;

/// <summary>A range of a person's diary, one entry per day whether or not anything was logged.</summary>
public sealed record Response
{
    /// <summary>The days from the first to the last asked for, oldest first.</summary>
    public required IReadOnlyList<DiaryDay> Days { get; init; }
}
