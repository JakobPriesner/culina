using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Cookbooks;

namespace Api.Endpoints.Cookbooks.RemoveRecipe.V1;

/// <summary>Takes a recipe off a shelf.</summary>
internal sealed class RemoveRecipeFromCookbookEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete(
                $"{ApiPaths.V1}/cookbooks/{{cookbookId:guid}}/recipes/{{recipeId:guid}}",
                async (
                    Guid cookbookId,
                    Guid recipeId,
                    HttpContext context,
                    ICommandHandler<RemoveRecipeFromCookbookCommand> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler
                        .Handle(
                            new RemoveRecipeFromCookbookCommand(
                                cookbookId,
                                recipeId,
                                context.CurrentUser().UserId),
                            cancellationToken)
                        .ConfigureAwait(false);

                    return result.Match(Results.NoContent, CustomResults.Problem);
                })
            .WithName("removeRecipeFromCookbookV1")
            .WithTags(Tags.Cookbooks)
            .WithSummary("Take a recipe off a cookbook")
            .WithDescription(
                "The recipe itself is untouched. Idempotent: taking off something that was never "
                + "on also answers 204. `409` on a cookbook that fills itself, which has nothing "
                + "to take off by hand.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
