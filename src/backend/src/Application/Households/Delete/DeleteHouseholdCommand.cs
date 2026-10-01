using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Households;
using Domain.Shared;

namespace Application.Households.Delete;

/// <summary>Puts a household and everything it owns in the bin. Owners only.</summary>
/// <param name="HouseholdId">Which household.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="ExpectedVersion">The version the caller was holding.</param>
public sealed record DeleteHouseholdCommand(Guid HouseholdId, Guid UserId, long ExpectedVersion);

internal sealed class DeleteHouseholdCommandHandler(
    IHouseholdRepository households,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<DeleteHouseholdCommand>
{
    public async Task<Result> Handle(
        DeleteHouseholdCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Households.Delete");

        var found = await households.FindAsync(command.HouseholdId, cancellationToken).ConfigureAwait(false);

        var permitted = found.Bind(household =>
            HouseholdMembershipPolicy.CanAdminister(household, command.UserId));

        var result = await permitted.Match(
            () => unitOfWork.InTransactionAsync(
                token => households.DeleteAsync(
                    command.HouseholdId, command.ExpectedVersion, command.UserId, time.GetUtcNow(), token),
                cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
