using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Households;
using Domain.Shared;

namespace Application.Households.RemoveMember;

/// <summary>Removes someone from a household, or leaves it.</summary>
/// <param name="HouseholdId">Which household.</param>
/// <param name="MemberId">Who is being removed.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record RemoveMemberCommand(Guid HouseholdId, Guid MemberId, Guid UserId);

internal sealed class RemoveMemberCommandHandler(
    IHouseholdRepository households,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveMemberCommand>
{
    public async Task<Result> Handle(
        RemoveMemberCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Households.RemoveMember");

        var found = await households.FindAsync(command.HouseholdId, cancellationToken).ConfigureAwait(false);

        // Who may remove whom, and the last-owner rule, live in the aggregate.
        var removed = found.Bind(household => household
            .Remove(command.MemberId, command.UserId)
            .Map(() => household));

        var result = await removed.Match(
            household => unitOfWork.InTransactionAsync(
                async token =>
                {
                    // The loaded version: this is a same-request membership change, so there is no If-Match.
                    var saved = await households
                        .UpdateAsync(household, household.Version, token)
                        .ConfigureAwait(false);

                    return saved.Match(_ => Result.Success(), Result.Failure);
                },
                cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
