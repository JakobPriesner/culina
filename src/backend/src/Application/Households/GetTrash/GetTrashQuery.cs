using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Households.GetTrash;
using Domain.Households;
using Domain.Shared;
using Domain.Trash;
using Response = Contracts.Households.GetTrash.Response;

namespace Application.Households.GetTrash;

/// <summary>What is in a household's bin.</summary>
/// <param name="HouseholdId">Which household.</param>
/// <param name="UserId">Who is asking; any member may look.</param>
public sealed record GetTrashQuery(Guid HouseholdId, Guid UserId);

internal sealed class GetTrashQueryHandler(ITrashRepository trash, IHouseholdRepository households)
    : IQueryHandler<GetTrashQuery, Response>
{
    public async Task<Result<Response>> Handle(GetTrashQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Households.GetTrash");

        var found = await households.FindAsync(query.HouseholdId, cancellationToken).ConfigureAwait(false);
        var permitted = found.Bind(household => HouseholdMembershipPolicy.CanView(household, query.UserId));

        var result = await permitted.Match(
            async () =>
            {
                var items = await trash.ForHouseholdAsync(query.HouseholdId, cancellationToken).ConfigureAwait(false);

                return Result<Response>.Success(new Response { Items = [.. items.Select(ToContract)] });
            },
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private static TrashItem ToContract(TrashedItem item) => new()
    {
        Kind = item.Kind == TrashedKind.Cookbook ? "cookbook" : "recipe",
        Id = item.Id,
        Name = item.Name,
        DeletedAt = item.DeletedAt,
        PurgeAfter = TrashPolicy.PurgeAfter(item.DeletedAt),
        DeletedBy = item.DeletedBy
    };
}
