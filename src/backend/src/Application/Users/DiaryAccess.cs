using Application.Abstractions;
using Domain.Diary;
using Domain.Shared;

namespace Application.Users;

/// <summary>The one gate every diary read and write passes: the person switched it on.</summary>
internal static class DiaryAccess
{
    /// <summary>Success when this person keeps a diary; switched off, the entries are kept but out of reach.</summary>
    internal static async Task<Result> EnabledAsync(
        IDiaryRepository diary,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var settings = await diary.SettingsAsync(userId, cancellationToken).ConfigureAwait(false);

        return settings.Enabled ? Result.Success() : DiaryErrors.Disabled;
    }
}
