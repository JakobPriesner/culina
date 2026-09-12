using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.Create;
using Contracts.Recipes;
using Request = Contracts.Recipes.Create.Request;

namespace Api.Endpoints.Recipes.Create.V1;

/// <summary>Starts a recipe.</summary>
internal sealed class CreateRecipeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/recipes", async (
                Request request,
                HttpContext context,
                ICommandHandler<CreateRecipeCommand, RecipeDetail> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new CreateRecipeCommand(
                            request.HouseholdId,
                            request.Title,
                            context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    created => Results.Created($"{ApiPaths.V1}/recipes/{created.RecipeId}", created),
                    CustomResults.Problem);
            })
            .WithName("createRecipeV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Start a recipe")
            .WithDescription(
                "Only a household and a title are required. Everything else is added later, which "
                + "is what makes the create form something people finish.")
            .Produces<RecipeDetail>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
