using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Households;
using Domain.Households;
using Domain.Shared;
using Response = Contracts.Households.GetAll.Response;

namespace Application.Households.GetAll;

/// <summary>Lists the households the caller belongs to.</summary>
/// <param name="UserId">Who is asking.</param>
public sealed record GetHouseholdsQuery(Guid UserId);

internal sealed class GetHouseholdsQueryHandler(IHouseholdRepository households)
    : IQueryHandler<GetHouseholdsQuery, Response>
{
    public async Task<Result<Response>> Handle(
        GetHouseholdsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Households.GetAll");

        var found = await households.ForUserAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        return tracked.Record(Result<Response>.Success(new Response
        {
            Items = [.. found.Select(household => household.ToSummary(query.UserId))]
        }));
    }
}

/// <summary>Maps a household onto the shape this operation returns.</summary>
internal static class HouseholdListMappings
{
    internal static HouseholdSummary ToSummary(this Household household, Guid callerId) =>
        new()
        {
            HouseholdId = household.Id,
            Name = household.Name.Value,
            MemberCount = household.Members.Count,
            YourRole = household.YourRoleIn(callerId),
            Version = household.Version
        };
}
