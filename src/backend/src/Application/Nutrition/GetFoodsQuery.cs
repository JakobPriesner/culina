using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Nutrition;
using Domain.Shared;

namespace Application.Nutrition;

/// <summary>Finds foods of the Bundeslebensmittelschlüssel by what was typed.</summary>
/// <param name="Query">What was typed; blank finds nothing.</param>
/// <param name="Limit">How many at most.</param>
public sealed record GetFoodsQuery(string? Query, int Limit);

internal sealed class GetFoodsQueryHandler(IFoodTable foods) : IQueryHandler<GetFoodsQuery, FoodsResponse>
{
    public Task<Result<FoodsResponse>> Handle(GetFoodsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Nutrition.GetFoods");

        var response = new FoodsResponse
        {
            Items =
            [
                .. foods
                    .Search(query.Query ?? string.Empty, query.Limit)
                    .Select(food => new FoodSummary(
                        food.Code,
                        food.NameDe,
                        food.NameEn,
                        food.Per100Grams.EnergyKcal))
            ]
        };

        return Task.FromResult(tracked.Record(Result<FoodsResponse>.Success(response)));
    }
}
