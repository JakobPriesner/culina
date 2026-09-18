using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.RevokeShare;

namespace Api.Endpoints.Recipes.RevokeShare.V1;

/// <summary>Stops sharing a recipe.</summary>
/// <remarks>
/// 204 whether or not it was shared: "this recipe is not published" is the
/// state the caller asked for, and a second tap is not a failure.
/// </remarks>
internal sealed class RevokeRecipeShareEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/recipes/{{recipeId:guid}}/share", async (
                Guid recipeId,
                HttpContext context,
                ICommandHandler<RevokeShareCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new RevokeShareCommand(recipeId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("revokeRecipeShareV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Stop sharing a recipe")
            .WithDescription(
                "Every link that was sent stops working immediately. Sharing again afterwards "
                + "mints a new one, so a link that was taken back stays dead.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
