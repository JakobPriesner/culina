using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Households;
using Domain.Shared;

namespace Application.Households.RemoveHeir;

/// <summary>Stops a household inheriting this one's recipes. Owners of this one only.</summary>
/// <param name="HouseholdId">The household being inherited from.</param>
/// <param name="HeirId">The household that inherits from it directly.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record RemoveHeirCommand(Guid HouseholdId, Guid HeirId, Guid UserId);

internal sealed class RemoveHeirCommandHandler(
    IHouseholdRepository households,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveHeirCommand>
{
    public async Task<Result> Handle(RemoveHeirCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Households.RemoveHeir");

        var result = await unitOfWork.InTransactionAsync(
            async token =>
            {
                var parent = await households.FindAsync(command.HouseholdId, token).ConfigureAwait(false);
                var heir = await households.FindAsync(command.HeirId, token).ConfigureAwait(false);

                // The parent first, and whether the caller may administer it:
                // somebody who is not in it learns that it does not exist,
                // before anything — even whether it exists — is said about
                // the heir.
                var cut = parent.Bind(from => HouseholdMembershipPolicy
                    .CanAdminister(from, command.UserId)
                    .Bind(() => heir.Bind(to => to
                        .StopInheritingFrom(from, command.UserId)
                        .Map(() => to))));

                return await cut.Match(
                    async household =>
                    {
                        var saved = await households
                            .UpdateAsync(household, household.Version, token)
                            .ConfigureAwait(false);

                        return saved.Match(_ => Result.Success(), Result.Failure);
                    },
                    error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
