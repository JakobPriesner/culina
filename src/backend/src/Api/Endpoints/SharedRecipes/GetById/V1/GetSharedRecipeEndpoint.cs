using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.GetShared;
using Response = Contracts.Recipes.GetShared.Response;

namespace Api.Endpoints.SharedRecipes.GetById.V1;

/// <summary>Serves a recipe to whoever follows its link.</summary>
/// <remarks>
/// The only read of somebody's data with no <c>RequireAuthorization()</c>: it never sees a recipe
/// id (the token is the identifier), so there is no id to substitute and no membership check to get
/// wrong. Its own tag and path keep "everything under /recipes needs a session" true.
/// </remarks>
internal sealed class GetSharedRecipeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/shared-recipes/{{token}}", async (
                string token,
                HttpContext context,
                IQueryHandler<GetSharedRecipeQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetSharedRecipeQuery(token), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    recipe => Served(context, recipe),
                    CustomResults.Problem);
            })
            .WithName("getSharedRecipeV1")
            .WithTags(Tags.SharedRecipes)
            .WithSummary("Read a shared recipe")
            .WithDescription(
                "No account needed: the token in the path is the whole of the authorisation. "
                + "The recipe comes without its household, its author or its version — everything "
                + "the page draws and nothing about whose kitchen it is.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitExtensions.SharedRecipe);
    }

    /// <summary>
    /// Kept out of shared caches: the address carries a credential, and a proxy that cached the
    /// answer would serve a household's recipe from a store nobody can revoke.
    /// </summary>
    private static IResult Served(HttpContext context, Response recipe)
    {
        context.Response.Headers.CacheControl = "private, no-store";

        return Results.Ok(recipe);
    }
}
