using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Cookbooks;
using Contracts.Cookbooks;

namespace Api.Endpoints.Cookbooks.GetRecipes.V1;

/// <summary>Which recipes are on a cookbook.</summary>
internal sealed class GetCookbookRecipesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/cookbooks/{{cookbookId:guid}}/recipes", async (
                Guid cookbookId,
                HttpContext context,
                IQueryHandler<GetCookbookRecipesQuery, CookbookRecipesResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new GetCookbookRecipesQuery(cookbookId, context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getCookbookRecipesV1")
            .WithTags(Tags.Cookbooks)
            .WithSummary("List which recipes are on a cookbook")
            .WithDescription(
                "Every recipe on it, by id, for either kind of cookbook — so a picker can mark what is "
                + "already on before anybody taps it. The members of `PUT` and `DELETE "
                + "/cookbooks/{cookbookId}/recipes/{recipeId}`. To read the recipes themselves, "
                + "use `GET /recipes?cookbookId=…`.")
            .Produces<CookbookRecipesResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
