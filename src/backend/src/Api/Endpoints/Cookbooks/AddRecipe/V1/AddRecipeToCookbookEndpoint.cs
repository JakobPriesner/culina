using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Cookbooks;

namespace Api.Endpoints.Cookbooks.AddRecipe.V1;

/// <summary>Puts a recipe on a shelf. <c>PUT</c> on the membership, so a double tap or retried request is harmless.</summary>
internal sealed class AddRecipeToCookbookEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut(
                $"{ApiPaths.V1}/cookbooks/{{cookbookId:guid}}/recipes/{{recipeId:guid}}",
                async (
                    Guid cookbookId,
                    Guid recipeId,
                    HttpContext context,
                    ICommandHandler<AddRecipeToCookbookCommand> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler
                        .Handle(
                            new AddRecipeToCookbookCommand(
                                cookbookId,
                                recipeId,
                                context.CurrentUser().UserId),
                            cancellationToken)
                        .ConfigureAwait(false);

                    return result.Match(Results.NoContent, CustomResults.Problem);
                })
            .WithName("addRecipeToCookbookV1")
            .WithTags(Tags.Cookbooks)
            .WithSummary("Put a recipe on a cookbook")
            .WithDescription(
                "Idempotent. A recipe already on the shelf keeps the moment it went on, and the "
                + "cookbook's version is not bumped — churning it would throw away every cached "
                + "copy to report that nothing happened.\n\n"
                + "`409` on a cookbook that fills itself: its rules are its whole membership, so a "
                + "recipe put on by hand would be one the next read did not return.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
