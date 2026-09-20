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
                if (!TryReadWidth(context, out var width))
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
                        delivery => Served(context, buffer, delivery),
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

    /// <summary>
    /// Sends the bytes, or says the client already has them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No-cache rather than an hour's freshness, which is what every other read
    /// in this app sends and what this one's own description always claimed to.
    /// A picture is replaced under the address it was served from — that is
    /// what setting a new one is — so a browser told it could reuse its copy
    /// for an hour showed the old picture for an hour, with nothing on the
    /// screen to suggest the new one had arrived.
    /// </para>
    /// <para>
    /// It costs a request per view and almost no bytes: the tag is the content
    /// hash, so an unchanged picture answers 304 and is not sent again.
    /// </para>
    /// </remarks>
    private static IResult Served(HttpContext context, MemoryStream buffer, ImageDelivery delivery)
    {
        var tag = $"\"{delivery.ContentHash}\"";

        context.Response.Headers.ETag = tag;
        context.Response.Headers.CacheControl = "private, no-cache";

        return ETag.Matches(context.Request.Headers.IfNoneMatch, tag)
            ? Results.StatusCode(StatusCodes.Status304NotModified)
            : Results.Bytes(buffer.ToArray(), "image/webp");
    }
}
