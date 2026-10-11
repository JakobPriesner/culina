using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Recipes.GetNutrition;
using Application.Telemetry;
using Contracts.Nutrition;
using Domain.Nutrition;
using Domain.Shared;

namespace Application.Nutrition;

/// <summary>Lists what a household has said about its ingredients: the foods it chose and the weights it set.</summary>
/// <param name="HouseholdId">Which household.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record GetIngredientFactsQuery(Guid HouseholdId, Guid UserId);

internal sealed class GetIngredientFactsQueryHandler(
    IHouseholdRepository households,
    INutritionCorrectionRepository choices,
    INutritionWeightRepository weights,
    IFoodTable foods)
    : IQueryHandler<GetIngredientFactsQuery, IngredientFactsResponse>
{
    public async Task<Result<IngredientFactsResponse>> Handle(
        GetIngredientFactsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Nutrition.GetIngredientFacts");

        var allowed = await HouseholdAccess
            .MemberOfAsync(households, query.HouseholdId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await allowed.Match(
            () => ReadAsync(query.HouseholdId, cancellationToken),
            error => Task.FromResult(Result<IngredientFactsResponse>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }

    private async Task<Result<IngredientFactsResponse>> ReadAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var corrections = await choices.AllAsync(householdId, cancellationToken).ConfigureAwait(false);
        var weighed = await weights.AllAsync(householdId, cancellationToken).ConfigureAwait(false);

        var items = corrections.Keys.Union(weighed.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(name => new IngredientFact
            {
                Name = name,
                Corrected = corrections.ContainsKey(name),
                Food = corrections.GetValueOrDefault(name) is { } code ? FoodOf(code) : null,
                Weights =
                [
                    .. weighed.GetValueOrDefault(name, new Dictionary<string, decimal>())
                        .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                        .Select(pair => new UnitWeight { Unit = pair.Key, Grams = pair.Value })
                ]
            });

        return new IngredientFactsResponse { Items = [.. items] };
    }

    // A code the table no longer has is shown as not counted rather than failing the whole list.
    private Contracts.Recipes.GetNutrition.NutritionFood? FoodOf(string code)
    {
        var entry = FoodNames.All.FirstOrDefault(one => one.Code == code);

        return foods.Find(code)?.ToContract(entry?.LabelDe, entry?.LabelEn);
    }
}
