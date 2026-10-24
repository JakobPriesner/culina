using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Shared;

namespace Application.Users.EraseDiary;

/// <summary>Deletes every entry of a person's diary, switched on or off; the settings stay.</summary>
/// <param name="UserId">Whose.</param>
public sealed record EraseDiaryCommand(Guid UserId);

internal sealed class EraseDiaryCommandHandler(IDiaryRepository diary) : ICommandHandler<EraseDiaryCommand>
{
    public async Task<Result> Handle(EraseDiaryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Users.EraseDiary");

        await diary.RemoveAllAsync(command.UserId, cancellationToken).ConfigureAwait(false);

        return tracked.Record(Result.Success());
    }
}
