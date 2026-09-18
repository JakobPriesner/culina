using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.GetShare;
using Response = Contracts.Recipes.Share.Response;

namespace Api.Endpoints.Recipes.GetShare.V1;

/// <summary>Reads the link a recipe is published behind.</summary>
/// <remarks>
/// A singleton sub-resource, because a recipe has at most one link and the
/// database says so. 404 is the ordinary answer and means "not shared" — which
/// is what the sheet renders as the off state.
/// </remarks>
internal sealed class GetRecipeShareEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/recipes/{{recipeId:guid}}/share", async (
                Guid recipeId,
                HttpContext context,
                IQueryHandler<GetShareQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetShareQuery(recipeId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getRecipeShareV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Read a recipe's share link")
            .WithDescription(
                "404 when the recipe is not shared, which is the ordinary answer. The token is "
                + "returned rather than a whole address: which origin Culina is reached at is the "
                + "browser's fact, not the server's.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
