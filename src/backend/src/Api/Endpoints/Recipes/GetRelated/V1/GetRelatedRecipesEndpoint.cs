using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.GetRelated;
using Response = Contracts.Recipes.GetRelated.Response;

namespace Api.Endpoints.Recipes.GetRelated.V1;

/// <summary>Says which recipes of the same household are most like this one.</summary>
internal sealed class GetRelatedRecipesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/recipes/{{recipeId:guid}}/related", async (
                Guid recipeId,
                HttpContext context,
                IQueryHandler<GetRelatedRecipesQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetRelatedRecipesQuery(recipeId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getRelatedRecipesV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Recipes like this one")
            .WithDescription(
                "Up to three recipes of the same household, the closest first, each with what it has in "
                + "common with this one: what they both are, or what they are both made from. Worked "
                + "out from the concepts the recipes are indexed under, and weighted by how rare each "
                + "is in the household. Empty when nothing is alike enough to say so. Not cached: it "
                + "changes with every other recipe in the household.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
