using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Recipes.GetNutrition;
using Domain.Nutrition;
using Domain.Shared;
using Domain.Shopping;

namespace Application.Recipes.GetNutrition;

/// <summary>Reads a recipe's nutrition as one household sees it.</summary>
/// <param name="RecipeId">The recipe.</param>
/// <param name="UserId">Who asks.</param>
/// <param name="HouseholdId">Whose corrections apply; null for the recipe's own household.</param>
public sealed record GetNutritionQuery(Guid RecipeId, Guid UserId, Guid? HouseholdId);

/// <summary>A nutrition figure with what its ETag must change with.</summary>
/// <param name="Body">The figure.</param>
/// <param name="RecipeVersion">The recipe's version.</param>
/// <param name="HouseholdId">The household whose corrections applied.</param>
/// <param name="DataVersion">The version of the data and rules the figure was computed with.</param>
/// <param name="Corrections">The corrections that applied to this recipe's names, as <c>name=code</c> or <c>name=none</c>.</param>
public sealed record RecipeNutrition(
    Response Body,
    long RecipeVersion,
    Guid HouseholdId,
    int DataVersion,
    IReadOnlyList<string> Corrections);

internal sealed class GetNutritionQueryHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IFoodTable foods,
    INutritionCorrectionRepository choices)
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

                // One indexed query for the names this recipe uses; the household passed, never the one
                // it inherits from.
                var corrections = await choices
                    .ForNamesAsync(
                        household,
                        [.. recipe.Ingredients.Select(line => ItemName.Fold(line.Name)).Distinct()],
                        cancellationToken)
                    .ConfigureAwait(false);

                var figure = NutritionCalculator.Calculate(recipe.Ingredients, recipe.Yield, foods.Find, corrections);

                return Result<RecipeNutrition>.Success(new RecipeNutrition(
                    figure.ToResponse(),
                    recipe.Version,
                    household,
                    NutritionData.Version,
                    [.. corrections.Select(pair => $"{pair.Key}={pair.Value ?? "none"}")]));
            },
            error => Task.FromResult(Result<RecipeNutrition>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}
