using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Telemetry;
using Contracts.Recipes.GetUnits;
using Domain.Recipes;
using Domain.Shared;

namespace Application.Recipes.GetUnits;

/// <summary>Reads the units a household can offer when writing a recipe.</summary>
/// <param name="HouseholdId">Whose kitchen.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record GetUnitsQuery(Guid HouseholdId, Guid UserId);

internal sealed class GetUnitsQueryHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households)
    : IQueryHandler<GetUnitsQuery, Response>
{
    public async Task<Result<Response>> Handle(
        GetUnitsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Recipes.GetUnits");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, query.HouseholdId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            async () => Result<Response>.Success(new Response
            {
                BuiltIn = [.. Unit.BuiltIn.Select(unit => unit.Code)],
                Own = await recipes
                    .OwnUnitsAsync(query.HouseholdId, cancellationToken)
                    .ConfigureAwait(false)
            }),
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
