using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Households;
using Domain.Shared;
using Response = Contracts.Households.CreateInvitation.Response;

namespace Application.Households.CreateInvitation;

/// <summary>Issues an invitation to a household. Owners only.</summary>
/// <param name="HouseholdId">Which household.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record CreateInvitationCommand(Guid HouseholdId, Guid UserId);

internal sealed class CreateInvitationCommandHandler(
    IHouseholdRepository households,
    IInvitationRepository invitations,
    ISecretTokens tokens,
    IUnitOfWork unitOfWork,
    TimeProvider time)
    : ICommandHandler<CreateInvitationCommand, Response>
{
    public async Task<Result<Response>> Handle(
        CreateInvitationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var tracked = UseCaseActivity.Start("Households.CreateInvitation");

        var found = await households.FindAsync(command.HouseholdId, cancellationToken).ConfigureAwait(false);

        var permitted = found.Bind(household =>
            HouseholdMembershipPolicy.CanAdminister(household, command.UserId));

        var result = await permitted.Match(
            () => IssueAsync(command, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> IssueAsync(
        CreateInvitationCommand command,
        CancellationToken cancellationToken)
    {
        var code = tokens.NewToken();

        var invitation = HouseholdInvitation.Issue(
            command.HouseholdId,
            tokens.Digest(code),
            command.UserId,
            time.GetUtcNow());

        return await unitOfWork.InTransactionAsync(
            async token =>
            {
                var added = await invitations.AddAsync(invitation, token).ConfigureAwait(false);

                // The code leaves here and is never recoverable: only its
                // digest is stored, so this response is the one chance to show
                // it.
                return added.Bind(() => Result<Response>.Success(new Response
                {
                    InvitationId = invitation.Id,
                    Code = code,
                    ExpiresAt = invitation.ExpiresAt
                }));
            },
            cancellationToken).ConfigureAwait(false);
    }
}
