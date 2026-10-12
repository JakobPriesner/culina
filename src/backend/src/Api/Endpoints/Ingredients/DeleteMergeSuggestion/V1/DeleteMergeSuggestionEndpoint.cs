using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Ingredients;

namespace Api.Endpoints.Ingredients.DeleteMergeSuggestion.V1;

/// <summary>Dismisses a merge suggestion: the name is its own ingredient.</summary>
internal sealed class DeleteMergeSuggestionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/households/{{householdId:guid}}/ingredient-merge-suggestions/{{name}}", async (
                Guid householdId,
                string name,
                HttpContext context,
                ICommandHandler<DeleteMergeSuggestionCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new DeleteMergeSuggestionCommand(
                            householdId,
                            context.CurrentUser().UserId,
                            PathSegments.LastDecoded(context, name)),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("deleteIngredientMergeSuggestionV1")
            .WithTags(Tags.Ingredients)
            .WithSummary("Dismiss an ingredient merge suggestion")
            .WithDescription(
                "Keeps `{name}` apart: it is never proposed for merging again, whatever it would be "
                + "merged into. `{name}` is the suggestion's `from`, percent-encoded. Idempotent; 204.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
