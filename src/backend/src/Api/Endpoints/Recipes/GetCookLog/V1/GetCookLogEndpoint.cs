using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.GetCookLog;
using Response = Contracts.Recipes.GetCookLog.Response;

namespace Api.Endpoints.Recipes.GetCookLog.V1;

/// <summary>Reads how often you have cooked a recipe.</summary>
internal sealed class GetCookLogEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/recipes/{{recipeId:guid}}/cook-log", async (
                Guid recipeId,
                HttpContext context,
                IQueryHandler<GetCookLogQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetCookLogQuery(recipeId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getRecipeCookLogV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Read your cooking history")
            .WithDescription(
                "How many times you have made this and when you last did. Personal, like notes: "
                + "two people in one household keep separate histories.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
