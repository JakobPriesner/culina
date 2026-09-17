using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.Sources;
using Contracts.Recipes.Sources;

namespace Api.Endpoints.RecipeSources.Browse.V1;

/// <summary>Reads a page of somebody else's library.</summary>
internal sealed class BrowseRecipeSourceEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/recipe-sources/{{sourceId:guid}}/recipes", async (
                Guid sourceId,
                string? page,
                string? query,
                HttpContext context,
                IQueryHandler<BrowseSourceQuery, SourceRecipesResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new BrowseSourceQuery(sourceId, context.CurrentUser().UserId, page, query),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("browseRecipeSourceV1")
            .WithTags(Tags.RecipeSources)
            .WithSummary("Read a page of a connected library")
            .WithDescription(
                "Summaries only — a name, a picture and a time — so that browsing two thousand "
                + "recipes does not fetch two thousand recipes. The full recipe is read only for "
                + "the ones actually chosen.\n\n"
                + "Every row says whether it is `alreadyHere`, and that is what makes coming back "
                + "next month cheap: you see what is new rather than the whole library again.\n\n"
                + "`page` is the opaque token from the previous response. It is checked against "
                + "the connection's own address before it is followed, because a token that has "
                + "been round-tripped through a client is user input.")
            .WithRepeatableQueryParameters(["page", "query"], [])
            .RequireRateLimiting(RateLimitExtensions.Import)
            .Produces<SourceRecipesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireAuthorization();
    }
}
