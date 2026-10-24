using Domain.Diary;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Reads and writes one person's food diary and its settings.</summary>
public interface IDiaryRepository
{
    /// <summary>This person's settings, or the defaults (off) when they never opened them.</summary>
    Task<DiarySettings> SettingsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Stores the settings at the version they carry.</summary>
    Task SaveSettingsAsync(DiarySettings settings, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>This person's entries from the first day to the last, inclusive, oldest first.</summary>
    Task<IReadOnlyList<DiaryEntry>> EntriesAsync(
        Guid userId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken);

    /// <summary>One of this person's entries, or not found.</summary>
    Task<Result<DiaryEntry>> FindAsync(Guid userId, Guid entryId, CancellationToken cancellationToken);

    /// <summary>Adds an entry.</summary>
    Task AddAsync(DiaryEntry entry, CancellationToken cancellationToken);

    /// <summary>Replaces an entry with its changed self.</summary>
    Task<Result> UpdateAsync(DiaryEntry entry, CancellationToken cancellationToken);

    /// <summary>Removes one of this person's entries.</summary>
    Task<Result> RemoveAsync(Guid userId, Guid entryId, CancellationToken cancellationToken);

    /// <summary>Removes every entry this person has.</summary>
    Task RemoveAllAsync(Guid userId, CancellationToken cancellationToken);
}
