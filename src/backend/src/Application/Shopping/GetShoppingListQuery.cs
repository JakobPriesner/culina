using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Shopping;
using Domain.Households;
using Domain.Shared;

namespace Application.Shopping;

/// <summary>Reads a household's list.</summary>
/// <param name="HouseholdId">Whose list.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record GetShoppingListQuery(Guid HouseholdId, Guid UserId);

internal sealed class GetShoppingListQueryHandler(
    IShoppingListRepository lists,
    IHouseholdRepository households)
    : IQueryHandler<GetShoppingListQuery, Response>
{
    public async Task<Result<Response>> Handle(
        GetShoppingListQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Shopping.Get");

        var member = await households
            .IsMemberAsync(query.HouseholdId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (!member)
        {
            // 404 rather than 403: a stranger learns nothing about which
            // households exist.
            return tracked.Record(Result<Response>.Failure(HouseholdErrors.NotFound(query.HouseholdId)));
        }

        var list = await lists.ForHouseholdAsync(query.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        return tracked.Record(list.Map(found => found.Describe()));
    }
}
