using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Households;
using Domain.Shared;
using Response = Contracts.Households.GetMembers.Response;

namespace Application.Households.GetMembers;

/// <summary>Lists a household's members.</summary>
/// <param name="HouseholdId">Which household.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record GetMembersQuery(Guid HouseholdId, Guid UserId);

internal sealed class GetMembersQueryHandler(IHouseholdRepository households)
    : IQueryHandler<GetMembersQuery, Response>
{
    public async Task<Result<Response>> Handle(
        GetMembersQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Households.GetMembers");

        var found = await households.FindAsync(query.HouseholdId, cancellationToken).ConfigureAwait(false);

        var visible = found.Bind(household =>
            HouseholdMembershipPolicy.CanView(household, query.UserId).Map(() => household));

        var result = await visible.Match(
            async household => Result<Response>.Success(new Response
            {
                Items =
                [
                    .. (await households.MembersAsync(household.Id, cancellationToken)
                        .ConfigureAwait(false))
                    .Select(member => member.ToContract())
                ]
            }),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
