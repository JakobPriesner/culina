using System.Globalization;
using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Recipes.GetImage;
using Application.Recipes.GetSharedImage;
using Domain.Recipes;

namespace Api.Endpoints.SharedRecipes.GetImage.V1;

/// <summary>Serves a shared recipe's photograph.</summary>
/// <remarks>
/// Under the token and not under the recipe's own image address, for the same
/// reason the recipe is: whoever follows a link holds the token and knows no
/// recipe id, and the id-addressed image stays behind the membership check it
/// has always had.
/// </remarks>
internal sealed class GetSharedRecipeImageEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/shared-recipes/{{token}}/image", async (
                string token,
                HttpContext context,
                IQueryHandler<GetSharedImageQuery, ImageDelivery> handler,
                CancellationToken cancellationToken) =>
            {
                if (!ImageResponse.TryReadWidth(context, out var width))
                {
                    return CustomResults.Problem(ImageErrors.UnknownWidth);
                }

                var buffer = new MemoryStream();

                await using (buffer.ConfigureAwait(false))
                {
                    var result = await handler
                        .Handle(new GetSharedImageQuery(token, width, buffer), cancellationToken)
                        .ConfigureAwait(false);

                    return result.Match(
                        delivery => ImageResponse.Served(context, buffer, delivery),
                        CustomResults.Problem);
                }
            })
            .WithName("getSharedRecipeImageV1")
            .WithTags(Tags.SharedRecipes)
            .WithSummary("Read a shared recipe's image")
            .WithDescription(
                "Widths 400, 800 and 1600, as for any recipe image. Private and revalidated: the "
                + "address carries a credential, so only the reader's own browser may keep it.")
            .WithRepeatableQueryParameters(["w"], [], ["w"])
            .Produces<byte[]>(StatusCodes.Status200OK, "image/webp")
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitExtensions.SharedRecipe);
    }


}
