using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Shared;

namespace Application.Users.RemoveDiaryEntry;

/// <summary>Removes one of a person's diary entries; the client offers the undo by logging it again.</summary>
/// <param name="UserId">Whose.</param>
/// <param name="EntryId">Which entry.</param>
public sealed record RemoveDiaryEntryCommand(Guid UserId, Guid EntryId);

internal sealed class RemoveDiaryEntryCommandHandler(IDiaryRepository diary)
    : ICommandHandler<RemoveDiaryEntryCommand>
{
    public async Task<Result> Handle(RemoveDiaryEntryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Users.RemoveDiaryEntry");

        var enabled = await DiaryAccess.EnabledAsync(diary, command.UserId, cancellationToken).ConfigureAwait(false);
        var result = await enabled.Match(
            () => diary.RemoveAsync(command.UserId, command.EntryId, cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
