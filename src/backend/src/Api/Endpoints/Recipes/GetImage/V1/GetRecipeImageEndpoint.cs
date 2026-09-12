using System.Globalization;
using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Recipes.GetImage;

namespace Api.Endpoints.Recipes.GetImage.V1;

/// <summary>Serves a recipe's image.</summary>
internal sealed class GetRecipeImageEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/recipes/{{recipeId:guid}}/image", async (
                Guid recipeId,
                HttpContext context,
                IQueryHandler<GetRecipeImageQuery, ImageDelivery> handler,
                CancellationToken cancellationToken) =>
            {
                if (!TryReadWidth(context, out var width))
                {
                    return CustomResults.Problem(Domain.Recipes.ImageErrors.UnknownWidth);
                }

                // The body is written straight to the response, so a large image
                // never sits in memory on its way out.
                var buffer = new MemoryStream();

                await using (buffer.ConfigureAwait(false))
                {
                    var result = await handler
                        .Handle(
                            new GetRecipeImageQuery(
                                recipeId,
                                context.CurrentUser().UserId,
                                width,
                                buffer),
                            cancellationToken)
                        .ConfigureAwait(false);

                    return result.Match(
                        delivery => Served(context, buffer, delivery),
                        CustomResults.Problem);
                }
            })
            .WithName("getRecipeImageV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Read a recipe image")
            .WithDescription(
                "Widths 400, 800 and 1600. Private and revalidated, because an image is exactly as "
                + "private as the recipe it belongs to; its ETag is the content hash, which cannot "
                + "change under the same address.")
            .WithRepeatableQueryParameters(["w"], [], ["w"])
            .Produces<byte[]>(StatusCodes.Status200OK, "image/webp")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }

    private static bool TryReadWidth(HttpContext context, out int width)
    {
        width = ImageWidths.Detail;

        if (context.Request.Query["w"].Count == 0)
        {
            return true;
        }

        return int.TryParse(context.Request.Query["w"], CultureInfo.InvariantCulture, out width)
            && ImageWidths.Exists(width);
    }

    private static IResult Served(HttpContext context, MemoryStream buffer, ImageDelivery delivery)
    {
        context.Response.Headers.ETag = $"\"{delivery.ContentHash}\"";
        context.Response.Headers.CacheControl = "private, max-age=3600";

        return Results.Bytes(buffer.ToArray(), "image/webp");
    }
}
