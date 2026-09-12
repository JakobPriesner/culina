using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Recipes;
using Domain.Shared;

namespace Application.Recipes.GetById;

/// <summary>Reads one recipe in full.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record GetRecipeQuery(Guid RecipeId, Guid UserId);

internal sealed class GetRecipeQueryHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households)
    : IQueryHandler<GetRecipeQuery, RecipeDetail>
{
    public async Task<Result<RecipeDetail>> Handle(
        GetRecipeQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Recipes.GetById");

        var found = await RecipeAccess
            .VisibleAsync(recipes, households, query.RecipeId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        return tracked.Record(found.Map(recipe => recipe.Describe()));
    }
}
