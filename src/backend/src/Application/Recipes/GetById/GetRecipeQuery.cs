using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Contracts.Recipes;
using Domain.Import;
using Domain.Shared;

namespace Application.Recipes.GetById;

/// <summary>Reads one recipe in full.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record GetRecipeQuery(Guid RecipeId, Guid UserId);

internal sealed class GetRecipeQueryHandler(
    IRecipeRepository recipes,
    IRecipeOriginRepository origins,
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

        var described = await found.Match(
            async recipe =>
            {
                // Read here and not in the repository: provenance belongs to a
                // minority of recipes, and folding it into the aggregate would
                // make every recipe carry a table most of them have no row in.
                var origin = await origins
                    .FindAsync(recipe.Id, cancellationToken)
                    .ConfigureAwait(false);

                return Result<RecipeDetail>.Success(
                    recipe.Describe(origin.Match(value => value, _ => (RecipeOrigin?)null)));
            },
            error => Task.FromResult(Result<RecipeDetail>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(described);
    }
}
