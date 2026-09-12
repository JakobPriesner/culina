using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Households;
using Domain.Shared;

namespace Application.Households.RevokeInvitation;

/// <summary>Deletes an invitation before it is used. Owners only.</summary>
/// <param name="HouseholdId">Which household.</param>
/// <param name="InvitationId">Which invitation.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record RevokeInvitationCommand(Guid HouseholdId, Guid InvitationId, Guid UserId);

internal sealed class RevokeInvitationCommandHandler(
    IHouseholdRepository households,
    IInvitationRepository invitations,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RevokeInvitationCommand>
{
    public async Task<Result> Handle(
        RevokeInvitationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Households.RevokeInvitation");

        var found = await households.FindAsync(command.HouseholdId, cancellationToken).ConfigureAwait(false);

        var permitted = found.Bind(household =>
            HouseholdMembershipPolicy.CanAdminister(household, command.UserId));

        var result = await permitted.Match(
            () => unitOfWork.InTransactionAsync(
                token => invitations.RevokeAsync(command.InvitationId, command.HouseholdId, token),
                cancellationToken),
            error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
