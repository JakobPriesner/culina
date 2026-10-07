using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Households;
using Domain.Shared;
using Response = Contracts.Households.GetInvitationByCode.Response;

namespace Application.Households.GetInvitationByCode;

/// <summary>Names the household an invitation code admits to, without using it.</summary>
/// <param name="Code">The code the caller was given.</param>
/// <remarks>
/// Lets the join page name the kitchen before anybody presses Join; an unknown, used or expired
/// code gets redeeming's error, so probing is no easier.
/// </remarks>
public sealed record GetInvitationByCodeQuery(string Code);

internal sealed class GetInvitationByCodeQueryHandler(
    IInvitationRepository invitations,
    IHouseholdRepository households,
    TimeProvider time)
    : IQueryHandler<GetInvitationByCodeQuery, Response>
{
    public async Task<Result<Response>> Handle(
        GetInvitationByCodeQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Households.GetInvitationByCode");

        var found = await invitations.FindByCodeAsync(query.Code, cancellationToken).ConfigureAwait(false);

        var usable = found.Bind(invitation => invitation.IsUsable(time.GetUtcNow())
            ? Result<Guid>.Success(invitation.HouseholdId)
            : HouseholdErrors.InvitationInvalid);

        var result = await usable.Match(
            householdId => NameAsync(householdId, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> NameAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var household = await households.FindAsync(householdId, cancellationToken).ConfigureAwait(false);

        // A household in the bin cannot be joined, and says so the same way.
        return household.Match(
            found => Result<Response>.Success(new Response { HouseholdName = found.Name.Value }),
            _ => Result<Response>.Failure(HouseholdErrors.InvitationInvalid));
    }
}
