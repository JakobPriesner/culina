using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Recipes.GetNutrition;
using Application.Telemetry;
using Contracts.Recipes.GetNutrition;
using Domain.Nutrition;
using Domain.Shared;

namespace Application.Recipes.GetSharedNutrition;

/// <summary>Reads a shared recipe's nutrition, for somebody who has nothing but the link.</summary>
/// <remarks>
/// Like <c>GetSharedRecipeQuery</c> there is no user behind it, and no household: the reader has
/// none, so no household's corrections apply and every line is read as the name table reads it.
/// </remarks>
public sealed record GetSharedNutritionQuery(string Token);

/// <summary>A shared recipe's nutrition with what its ETag must change with.</summary>
/// <param name="Body">The figure.</param>
/// <param name="RecipeVersion">The recipe's version.</param>
/// <param name="DataVersion">The version of the data and rules the figure was computed with.</param>
public sealed record SharedNutrition(Response Body, long RecipeVersion, int DataVersion);

internal sealed class GetSharedNutritionQueryHandler(
    IRecipeShareRepository shares,
    IRecipeRepository recipes,
    IFoodTable foods)
    : IQueryHandler<GetSharedNutritionQuery, SharedNutrition>
{
    private static readonly IReadOnlyDictionary<string, string?> NoCorrections = new Dictionary<string, string?>();

    public async Task<Result<SharedNutrition>> Handle(
        GetSharedNutritionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Recipes.GetSharedNutrition");

        var share = await shares
            .FindByTokenAsync(query.Token, cancellationToken)
            .ConfigureAwait(false);

        var found = await share.Match(
            link => recipes.FindAsync(link.RecipeId, cancellationToken),
            error => Task.FromResult(Result<Domain.Recipes.Recipe>.Failure(error)))
            .ConfigureAwait(false);

        var result = found.Map(recipe => new SharedNutrition(
            NutritionCalculator.Calculate(recipe.Ingredients, recipe.Yield, foods.Find, NoCorrections).ToResponse(),
            recipe.Version,
            NutritionData.Version));

        return tracked.Record(result);
    }
}
