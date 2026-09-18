using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Shared;
using Response = Contracts.Recipes.Share.Response;

namespace Application.Recipes.GetShare;

/// <summary>Reads the link a recipe is published behind, if it has one.</summary>
/// <param name="RecipeId">Which recipe.</param>
/// <param name="UserId">Who is asking.</param>
public sealed record GetShareQuery(Guid RecipeId, Guid UserId);

internal sealed class GetShareQueryHandler(
    IRecipeRepository recipes,
    IHouseholdRepository households,
    IRecipeShareRepository shares)
    : IQueryHandler<GetShareQuery, Response>
{
    public async Task<Result<Response>> Handle(
        GetShareQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Recipes.GetShare");

        // Through the same gate as reading the recipe: the token is a key to
        // it, so anyone who may not read the recipe may certainly not read the
        // key — and gets told the recipe does not exist, as everywhere else.
        var visible = await RecipeAccess
            .VisibleAsync(recipes, households, query.RecipeId, query.UserId, cancellationToken)
            .ConfigureAwait(false);

        var found = await visible.Match(
            recipe => shares.FindAsync(recipe.Id, cancellationToken),
            error => Task.FromResult(Result<Domain.Recipes.RecipeShare>.Failure(error)))
            .ConfigureAwait(false);

        return tracked.Record(found.Map(share => new Response
        {
            Token = share.Token,
            CreatedAt = share.CreatedAt
        }));
    }
}
