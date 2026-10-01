using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Households;
using Domain.Shared;

namespace Application.Households.Restore;

/// <summary>
/// Takes a household out of the bin, with its members, recipes and cookbooks.
/// Owners only, as deleting it was.
/// </summary>
/// <param name="HouseholdId">Which household.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record RestoreHouseholdCommand(Guid HouseholdId, Guid UserId);

internal sealed class RestoreHouseholdCommandHandler(
    ITrashRepository trash,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RestoreHouseholdCommand>
{
    public async Task<Result> Handle(RestoreHouseholdCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Households.Restore");

        var deleted = await trash.DeletedHouseholdAsync(command.HouseholdId, cancellationToken).ConfigureAwait(false);

        // The memberships were kept for exactly this: the same rule decides
        // who may bring a household back as decided who could delete it.
        Result permitted = deleted is null
            ? HouseholdErrors.NotFound(command.HouseholdId)
            : HouseholdMembershipPolicy.CanAdminister(deleted, command.UserId);

        var result = await permitted.Match(
            () => unitOfWork.InTransactionAsync(
                async token => await trash.RestoreHouseholdAsync(command.HouseholdId, token).ConfigureAwait(false)
                    ? Result.Success()
                    : Result.Failure(HouseholdErrors.NotFound(command.HouseholdId)),
                cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
