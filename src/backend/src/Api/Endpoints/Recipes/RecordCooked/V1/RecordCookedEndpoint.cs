using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.RecordCooked;
using Request = Contracts.Recipes.RecordCooked.Request;
using Response = Contracts.Recipes.RecordCooked.Response;

namespace Api.Endpoints.Recipes.RecordCooked.V1;

/// <summary>Records that you cooked a recipe.</summary>
internal sealed class RecordCookedEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/recipes/{{recipeId:guid}}/cook-log", async (
                Guid recipeId,
                Request? request,
                HttpContext context,
                ICommandHandler<RecordCookedCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new RecordCookedCommand(
                            recipeId,
                            context.CurrentUser().UserId,
                            request?.MadeAt,
                            request?.Servings,
                            request?.Note,
                            request?.HouseholdId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    recorded => Results.Created(
                        $"{ApiPaths.V1}/recipes/{recipeId}/cook-log",
                        recorded),
                    CustomResults.Problem);
            })
            .WithName("recordRecipeCookedV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Say you made it")
            .WithDescription(
                "The body may be empty: one tap is the whole interaction. The response carries the "
                + "new count and the entry id, so the confirmation can say \"that's the 8th time\" "
                + "and offer an undo.")
            .Produces<Response>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
