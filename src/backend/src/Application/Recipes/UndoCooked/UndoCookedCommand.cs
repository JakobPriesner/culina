using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Shared;

namespace Application.Recipes.UndoCooked;

/// <summary>Takes back a "made it".</summary>
/// <param name="EntryId">Which entry.</param>
/// <param name="UserId">Whose it must be.</param>
/// <remarks>
/// This is the undo behind the confirmation toast, which is how Culina avoids
/// asking "are you sure?" before a one-tap action that was never dangerous.
/// </remarks>
public sealed record UndoCookedCommand(Guid EntryId, Guid UserId);

internal sealed class UndoCookedCommandHandler(ICookLogRepository entries)
    : ICommandHandler<UndoCookedCommand>
{
    public async Task<Result> Handle(
        UndoCookedCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Recipes.UndoCooked");

        var removed = await entries
            .RemoveAsync(command.EntryId, command.UserId, cancellationToken)
            .ConfigureAwait(false);

        return tracked.Record(removed);
    }
}
