using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Households;
using Domain.Shared;
using Response = Contracts.Households.GetById.Response;

namespace Application.Households.GetById;

/// <summary>Reads one household with its members.</summary>
/// <param name="HouseholdId">Which household.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record GetHouseholdQuery(Guid HouseholdId, Guid UserId);

internal sealed class GetHouseholdQueryHandler(IHouseholdRepository households)
    : IQueryHandler<GetHouseholdQuery, Response>
{
    public async Task<Result<Response>> Handle(
        GetHouseholdQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Households.GetById");

        var found = await households.FindAsync(query.HouseholdId, cancellationToken).ConfigureAwait(false);

        // A non-member gets not-found, never forbidden: answering "forbidden"
        // would confirm the household exists.
        var visible = found.Bind(household =>
            HouseholdMembershipPolicy.CanView(household, query.UserId).Map(() => household));

        var result = await visible.Match(
            household => BuildAsync(household, query.UserId, cancellationToken),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<Response>> BuildAsync(
        Household household,
        Guid callerId,
        CancellationToken cancellationToken)
    {
        var members = await households.MembersAsync(household.Id, cancellationToken).ConfigureAwait(false);

        return new Response
        {
            HouseholdId = household.Id,
            Name = household.Name.Value,
            MemberCount = members.Count,
            YourRole = household.YourRoleIn(callerId),
            CreatedAt = household.CreatedAt,
            Members = [.. members.Select(member => member.ToContract())],
            Version = household.Version
        };
    }
}
