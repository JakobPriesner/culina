using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Ingredients;

namespace Api.Endpoints.Ingredients.DeleteMerge.V1;

/// <summary>Forgets an ingredient merge.</summary>
internal sealed class DeleteIngredientMergeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/households/{{householdId:guid}}/ingredient-merges/{{from}}", async (
                Guid householdId,
                string from,
                HttpContext context,
                ICommandHandler<DeleteIngredientMergeCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new DeleteIngredientMergeCommand(
                            householdId,
                            context.CurrentUser().UserId,
                            PathSegments.LastDecoded(context, from)),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("deleteIngredientMergeV1")
            .WithTags(Tags.Ingredients)
            .WithSummary("Forget an ingredient merge")
            .WithDescription(
                "Stops writing `{from}` as the name it was merged into. Recipes already rewritten keep "
                + "the kept name; nothing is renamed back. `{from}` is the merged name, percent-encoded. "
                + "Idempotent; 204.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
