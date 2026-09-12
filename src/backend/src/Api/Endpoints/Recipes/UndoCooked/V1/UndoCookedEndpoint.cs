using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.UndoCooked;

namespace Api.Endpoints.Recipes.UndoCooked.V1;

/// <summary>Takes back a "made it".</summary>
internal sealed class UndoCookedEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete(
                $"{ApiPaths.V1}/recipes/{{recipeId:guid}}/cook-log/{{entryId:guid}}",
                async (
                    Guid entryId,
                    HttpContext context,
                    ICommandHandler<UndoCookedCommand> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler
                        .Handle(
                            new UndoCookedCommand(entryId, context.CurrentUser().UserId),
                            cancellationToken)
                        .ConfigureAwait(false);

                    return result.Match(Results.NoContent, CustomResults.Problem);
                })
            .WithName("undoRecipeCookedV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Take back a “made it”")
            .WithDescription(
                "The undo behind the confirmation toast. Culina does not ask "
                + "\"are you sure?\" before a one-tap action that was never dangerous; it does "
                + "the thing and offers this instead.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
