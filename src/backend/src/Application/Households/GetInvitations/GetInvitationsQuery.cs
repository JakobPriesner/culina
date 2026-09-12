using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Households.GetInvitations;
using Domain.Households;
using Domain.Shared;

namespace Application.Households.GetInvitations;

/// <summary>Lists a household's open invitations. Owners only.</summary>
/// <param name="HouseholdId">Which household.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record GetInvitationsQuery(Guid HouseholdId, Guid UserId);

internal sealed class GetInvitationsQueryHandler(
    IHouseholdRepository households,
    IInvitationRepository invitations)
    : IQueryHandler<GetInvitationsQuery, Response>
{
    public async Task<Result<Response>> Handle(
        GetInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Households.GetInvitations");

        var found = await households.FindAsync(query.HouseholdId, cancellationToken).ConfigureAwait(false);

        var permitted = found.Bind(household =>
            HouseholdMembershipPolicy.CanAdminister(household, query.UserId));

        var result = await permitted.Match(
            async () => Result<Response>.Success(new Response
            {
                Items =
                [
                    .. (await invitations.OpenForHouseholdAsync(query.HouseholdId, cancellationToken)
                        .ConfigureAwait(false))
                    .Select(invitation => new InvitationSummary
                    {
                        InvitationId = invitation.Id,
                        CreatedBy = invitation.CreatedBy,
                        CreatedAt = invitation.CreatedAt,
                        ExpiresAt = invitation.ExpiresAt
                    })
                ]
            }),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
