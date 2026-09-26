using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.Copy;
using Contracts.Recipes;
using Request = Contracts.Recipes.Copy.Request;

namespace Api.Endpoints.Recipes.Copy.V1;

/// <summary>Makes a household its own copy of a recipe.</summary>
/// <remarks>
/// A copy is created, so it is a sub-resource that is posted to rather than a
/// verb in the path: <c>POST /recipes/{id}/copies</c> answers with the new
/// recipe and where it lives.
/// </remarks>
internal sealed class CopyRecipeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/recipes/{{recipeId:guid}}/copies", async (
                Guid recipeId,
                Request request,
                HttpContext context,
                ICommandHandler<CopyRecipeCommand, RecipeDetail> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new CopyRecipeCommand(recipeId, request.HouseholdId, context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    created => Results.Created($"{ApiPaths.V1}/recipes/{created.RecipeId}", created),
                    CustomResults.Problem);
            })
            .WithName("copyRecipeV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Copy a recipe into a household")
            .WithDescription(
                "Any recipe you can read, into any household you are in — how a household changes a "
                + "recipe it only inherits. The copy has its own ingredient lines and steps, and "
                + "shares the picture; nothing links it back to the original.")
            .Produces<RecipeDetail>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
