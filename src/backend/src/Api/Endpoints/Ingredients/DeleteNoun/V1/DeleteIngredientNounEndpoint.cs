using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Ingredients;

namespace Api.Endpoints.Ingredients.DeleteNoun.V1;

/// <summary>Forgets an ingredient word's plural.</summary>
internal sealed class DeleteIngredientNounEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/households/{{householdId:guid}}/ingredient-nouns/{{one}}", async (
                Guid householdId,
                string one,
                HttpContext context,
                ICommandHandler<DeleteIngredientNounCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new DeleteIngredientNounCommand(
                            householdId,
                            context.CurrentUser().UserId,
                            PathSegments.LastDecoded(context, one)),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("deleteIngredientNounV1")
            .WithTags(Tags.Ingredients)
            .WithSummary("Forget an ingredient word's plural")
            .WithDescription(
                "Stops changing `{one}`, a singular, by the amount. Names already on the shopping list "
                + "keep their words. `{one}` is percent-encoded. Idempotent; 204.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
