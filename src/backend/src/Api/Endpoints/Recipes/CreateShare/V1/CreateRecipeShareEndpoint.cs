using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.CreateShare;
using Response = Contracts.Recipes.Share.Response;

namespace Api.Endpoints.Recipes.CreateShare.V1;

/// <summary>Publishes a recipe behind a link.</summary>
/// <remarks>
/// <c>PUT</c> and not <c>POST</c>, because it is idempotent and has to be:
/// pressing share a second time must hand back the address that was already
/// sent to somebody, never a new one that quietly orphans the old.
/// </remarks>
internal sealed class CreateRecipeShareEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/recipes/{{recipeId:guid}}/share", async (
                Guid recipeId,
                HttpContext context,
                ICommandHandler<CreateShareCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new CreateShareCommand(recipeId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("createRecipeShareV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Share a recipe by link")
            .WithDescription(
                "Anyone holding the returned link may read this one recipe without an account. "
                + "Idempotent: a recipe already shared keeps the link it has.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
