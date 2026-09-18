using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.GetShared;
using Response = Contracts.Recipes.GetShared.Response;

namespace Api.Endpoints.SharedRecipes.GetById.V1;

/// <summary>Serves a recipe to whoever follows its link.</summary>
/// <remarks>
/// <para>
/// The only endpoint in Culina with no <c>RequireAuthorization()</c> on a read
/// of somebody's data, and it earns that by never seeing a recipe id: the token
/// is the identifier, so there is no id to substitute and no membership check
/// to get wrong. A request without a valid token can address nothing.
/// </para>
/// <para>
/// Its own tag and its own path rather than a second route into
/// <c>/recipes</c>, so that "everything under /recipes needs a session" stays
/// true by reading the routes.
/// </para>
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
    /// Kept out of shared caches.
    /// </summary>
    /// <remarks>
    /// The address carries a credential, so nothing between here and the reader
    /// may hold on to the answer: a proxy that cached it would be serving one
    /// household's recipe from a store nobody can revoke.
    /// </remarks>
    private static IResult Served(HttpContext context, Response recipe)
    {
        context.Response.Headers.CacheControl = "private, no-store";

        return Results.Ok(recipe);
    }
}
