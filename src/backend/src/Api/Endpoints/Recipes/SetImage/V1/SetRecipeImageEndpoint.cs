using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.SetImage;
using Contracts.Recipes;

namespace Api.Endpoints.Recipes.SetImage.V1;

/// <summary>Uploads a recipe's image.</summary>
internal sealed class SetRecipeImageEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/recipes/{{recipeId:guid}}/image", async (
                Guid recipeId,
                IFormFile file,
                HttpContext context,
                ICommandHandler<SetRecipeImageCommand, RecipeDetail> handler,
                CancellationToken cancellationToken) =>
            {
                var content = file.OpenReadStream();

                await using (content.ConfigureAwait(false))
                {
                    var result = await handler
                        .Handle(
                            new SetRecipeImageCommand(recipeId, context.CurrentUser().UserId, content),
                            cancellationToken)
                        .ConfigureAwait(false);

                    return result.Match(Results.Ok, CustomResults.Problem);
                }
            })
            .WithName("setRecipeImageV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Upload a recipe image")
            .WithDescription(
                "The upload is decoded to find out what it is — never trusted by content type or "
                + "extension — and re-encoded to WebP at three widths. The bytes that are served "
                + "are always Culina's own re-encoding, which strips EXIF (food photos carry GPS "
                + "coordinates) and neutralises a file that is valid in two formats at once.")
            .Produces<RecipeDetail>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .DisableAntiforgery()
            .RequireAuthorization();
    }
}
