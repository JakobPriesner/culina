using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Shared;
using Domain.Trash;

namespace Application.Trash.Purge;

/// <summary>Removes for good whatever has been in a bin longer than the retention.</summary>
public sealed record PurgeTrashCommand(DateTimeOffset Now);

internal sealed class PurgeTrashCommandHandler(
    ITrashRepository trash,
    IImageStore images,
    IUnitOfWork unitOfWork)
    : ICommandHandler<PurgeTrashCommand, int>
{
    public async Task<Result<int>> Handle(PurgeTrashCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Trash.Purge");

        var purged = await unitOfWork.InTransactionAsync(
            token => trash.PurgeAsync(TrashPolicy.PurgeCutoff(command.Now), token),
            cancellationToken).ConfigureAwait(false);

        // After the commit, never inside it: a file deleted for a rolled-back transaction is a
        // broken picture, one left by a failure is only storage a later sweep reclaims.
        foreach (var hash in purged.ReleasedImages)
        {
            await images.DeleteAsync(hash, cancellationToken).ConfigureAwait(false);
        }

        return tracked.Record(Result<int>.Success(purged.Removed));
    }
}
