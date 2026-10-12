using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Ingredients;
using Request = Contracts.Ingredients.SetIngredientNounRequest;

namespace Api.Endpoints.Ingredients.SetNoun.V1;

/// <summary>Lists an ingredient word in both numbers.</summary>
internal sealed class SetIngredientNounEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/households/{{householdId:guid}}/ingredient-nouns/{{one}}", async (
                Guid householdId,
                string one,
                Request request,
                HttpContext context,
                ICommandHandler<SetIngredientNounCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new SetIngredientNounCommand(
                            householdId,
                            context.CurrentUser().UserId,
                            PathSegments.LastDecoded(context, one),
                            request.Many),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("setIngredientNounV1")
            .WithTags(Tags.Ingredients)
            .WithSummary("Say an ingredient word's plural")
            .WithDescription(
                "Lists `{one}`, a singular, with its plural, for this household only. Both are one word "
                + "each and differ. A word ending in it takes its forms too, as \"Zehe\" covers "
                + "\"Knoblauchzehe\". Setting it again changes the plural. `{one}` is percent-encoded. "
                + "Any member may set it. Idempotent; 204.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
