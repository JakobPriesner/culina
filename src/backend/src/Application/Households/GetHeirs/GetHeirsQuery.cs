using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Shared;
using Response = Contracts.Households.GetHeirs.Response;

namespace Application.Households.GetHeirs;

/// <summary>Lists the households that see a household's recipes.</summary>
/// <param name="HouseholdId">Which household.</param>
/// <param name="UserId">Who is asking.</param>
/// <remarks>
/// Any member may ask. Everybody in a kitchen is entitled to know who else
/// reads its recipes, even though only an owner can do anything about it.
/// </remarks>
public sealed record GetHeirsQuery(Guid HouseholdId, Guid UserId);

internal sealed class GetHeirsQueryHandler(IHouseholdRepository households)
    : IQueryHandler<GetHeirsQuery, Response>
{
    public async Task<Result<Response>> Handle(GetHeirsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Households.GetHeirs");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, query.HouseholdId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            async () =>
            {
                var heirs = await households.HeirsAsync(query.HouseholdId, cancellationToken).ConfigureAwait(false);

                return Result<Response>.Success(new Response
                {
                    Items = [.. heirs.Select(heir => new Contracts.Households.GetHeirs.Heir
                    {
                        HouseholdId = heir.HouseholdId,
                        Name = heir.Name,
                        InheritsFrom = heir.InheritsFrom
                    })]
                });
            },
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
