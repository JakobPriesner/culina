using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Households;
using Application.Telemetry;
using Contracts.Recipes.GetNutrition;
using Domain.Nutrition;
using Domain.Shared;
using Domain.Shopping;

namespace Application.Recipes.GetNutrition;

/// <summary>Reads a recipe's nutrition as one household sees it.</summary>
/// <param name="RecipeId">The recipe.</param>
/// <param name="UserId">Who asks.</param>
/// <param name="HouseholdId">Whose corrections apply, with those of the households it inherits from; null for the recipe's own household.</param>
public sealed record GetNutritionQuery(Guid RecipeId, Guid UserId, Guid? HouseholdId);

/// <summary>A nutrition figure with what its ETag must change with.</summary>
/// <param name="Body">The figure.</param>
/// <param name="RecipeVersion">The recipe's version.</param>
/// <param name="HouseholdId">The household whose corrections applied.</param>
/// <param name="DataVersion">The version of the data and rules the figure was computed with.</param>
/// <param name="Facts">
/// What the household and the households it inherits from said that applied to this recipe's names, one string each:
/// <c>name=code@household</c> or <c>name=none@household</c> for a food, <c>name/unit=grams@household</c> for a weight,
/// and whether typical weights count.
/// </param>
public sealed record RecipeNutrition(
    Response Body,
    long RecipeVersion,
    Guid HouseholdId,
    int DataVersion,
    IReadOnlyList<string> Facts);

internal sealed class GetNutritionQueryHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IFoodTable foods,
    INutritionCorrectionRepository choices,
    INutritionWeightRepository weights)
    : IQueryHandler<GetNutritionQuery, RecipeNutrition>
{
    public async Task<Result<RecipeNutrition>> Handle(
        GetNutritionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Recipes.GetNutrition");

        var found = await RecipeAccess
            .VisibleInAsync(recipes, households, query.RecipeId, query.HouseholdId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var result = await found.Match(
            async recipe =>
            {
                var household = query.HouseholdId ?? recipe.HouseholdId;
                var library = await HouseholdAccess
                    .LibraryAsync(households, household, cancellationToken)
                    .ConfigureAwait(false);

                // One indexed query each for the names this recipe uses: what the household said, else what the
                // households it inherits from said, nearest first.
                var nameOf = recipe.Ingredients.ToDictionary(line => line.Id, line => ItemName.Fold(line.Name));
                var names = nameOf.Values.Distinct().ToList();
                var corrections = await choices.ForNamesAsync(library, names, cancellationToken).ConfigureAwait(false);
                var weighed = await weights.ForNamesAsync(library, names, cancellationToken).ConfigureAwait(false);
                var useTypical = await weights.UsesTypicalWeightsAsync(household, cancellationToken).ConfigureAwait(false);

                var figure = NutritionCalculator.Calculate(
                    recipe.Ingredients,
                    recipe.Yield,
                    foods.Find,
                    corrections.ToDictionary(pair => pair.Key, pair => pair.Value.FoodCode, StringComparer.Ordinal),
                    weighed.ToDictionary(
                        pair => pair.Key,
                        pair => (IReadOnlyDictionary<string, decimal>)pair.Value.ToDictionary(
                            unit => unit.Key,
                            unit => unit.Value.Grams),
                        StringComparer.Ordinal),
                    useTypical);

                Whose WhoseIs(NutritionLine line)
                {
                    var name = nameOf[line.IngredientId];

                    return new Whose(
                        corrections.GetValueOrDefault(name)?.HouseholdId,
                        line.UnitKey is { } unit ? weighed.GetValueOrDefault(name)?.GetValueOrDefault(unit)?.HouseholdId : null);
                }

                return Result<RecipeNutrition>.Success(new RecipeNutrition(
                    figure.ToResponse(WhoseIs),
                    recipe.Version,
                    household,
                    NutritionData.Version,
                    [
                        .. corrections.Select(pair => $"{pair.Key}={pair.Value.FoodCode ?? "none"}@{pair.Value.HouseholdId}"),
                        .. weighed.SelectMany(name => name.Value.Select(unit =>
                            $"{name.Key}/{unit.Key}={unit.Value.Grams}@{unit.Value.HouseholdId}")),
                        $"typical={(useTypical ? "on" : "off")}"
                    ]));
            },
            error => Task.FromResult(Result<RecipeNutrition>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
