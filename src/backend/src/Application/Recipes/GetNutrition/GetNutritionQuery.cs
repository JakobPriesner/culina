using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Recipes.GetNutrition;
using Domain.Nutrition;
using Domain.Shared;

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
    IFoodTable foods)
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

        var result = found.Map(recipe =>
        {
            // Phase 4 reads the household's corrections for this recipe's names here.
            var corrections = new Dictionary<string, string?>();
            var figure = NutritionCalculator.Calculate(recipe.Ingredients, recipe.Yield, foods.Find, corrections);

            return new RecipeNutrition(
                figure.ToResponse(),
                recipe.Version,
                query.HouseholdId ?? recipe.HouseholdId,
                NutritionData.Version,
                [.. corrections.Select(pair => $"{pair.Key}={pair.Value ?? "none"}")]);
        });

        return tracked.Record(result);
    }
}
