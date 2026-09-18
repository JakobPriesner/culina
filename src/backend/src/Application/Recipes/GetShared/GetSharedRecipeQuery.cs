using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Telemetry;
using Domain.Import;
using Domain.Shared;
using Response = Contracts.Recipes.GetShared.Response;

namespace Application.Recipes.GetShared;

/// <summary>
/// Reads a published recipe, for somebody who has nothing but the link.
/// </summary>
/// <remarks>
/// The one read in Culina with no user behind it. There is deliberately no
/// <c>UserId</c> to forget to check: possession of the token is the whole of
/// the authorisation, so the shape of this query is the reason the check cannot
/// be skipped by accident.
/// </remarks>
/// <param name="Token">The secret out of the link.</param>
public sealed record GetSharedRecipeQuery(string Token);

internal sealed class GetSharedRecipeQueryHandler(
    IRecipeShareRepository shares,
    IRecipeRepository recipes,
    IRecipeOriginRepository origins)
    : IQueryHandler<GetSharedRecipeQuery, Response>
{
    public async Task<Result<Response>> Handle(
        GetSharedRecipeQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Recipes.GetShared");

        var share = await shares
            .FindByTokenAsync(query.Token, cancellationToken)
            .ConfigureAwait(false);

        var found = await share.Match(
            link => recipes.FindAsync(link.RecipeId, cancellationToken),
            error => Task.FromResult(Result<Domain.Recipes.Recipe>.Failure(error)))
            .ConfigureAwait(false);

        var described = await found.Match(
            async recipe =>
            {
                var origin = await origins
                    .FindAsync(recipe.Id, cancellationToken)
                    .ConfigureAwait(false);

                return Result<Response>.Success(
                    recipe.Publish(origin.Match(value => value, _ => (RecipeOrigin?)null)));
            },
            error => Task.FromResult(Result<Response>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(described);
    }
}
