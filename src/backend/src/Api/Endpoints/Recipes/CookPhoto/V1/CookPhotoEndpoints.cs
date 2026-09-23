using System.Globalization;
using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Cooking.CookPhoto;
using Application.Recipes.GetImage;
using Response = Contracts.Recipes.GetCookLog.Response;

namespace Api.Endpoints.Recipes.CookPhoto.V1;

/// <summary>
/// The picture of one attempt: put there, taken away, and served.
/// </summary>
/// <remarks>
/// Hung off the cook-log entry rather than the recipe, because that is what it
/// is a picture of — a Tuesday, not a dish. Personal, like the entry: two people
/// in one household keep separate histories and separate photographs of them.
/// </remarks>
internal sealed class SetCookPhotoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/recipes/{{recipeId:guid}}/cook-log/{{entryId:guid}}/photo", async (
                Guid entryId,
                IFormFile file,
                HttpContext context,
                ICommandHandler<SetCookPhotoCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var content = file.OpenReadStream();

                await using (content.ConfigureAwait(false))
                {
                    var result = await handler
                        .Handle(
                            new SetCookPhotoCommand(entryId, context.CurrentUser().UserId, content),
                            cancellationToken)
                        .ConfigureAwait(false);

                    return result.Match(Results.Ok, CustomResults.Problem);
                }
            })
            .WithName("setCookPhotoV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Add a photo of how yours turned out")
            .WithDescription(
                "Yours, not the household's: the recipe's own photograph is what the dish is "
                + "supposed to look like, and this is what it looked like on the day. Decoded to "
                + "find out what it is and re-encoded to WebP, which strips the EXIF a phone "
                + "photograph carries — including where the kitchen is.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .DisableAntiforgery()
            .RequireAuthorization();
    }
}

/// <summary>Takes the picture off an attempt.</summary>
internal sealed class RemoveCookPhotoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/recipes/{{recipeId:guid}}/cook-log/{{entryId:guid}}/photo", async (
                Guid entryId,
                HttpContext context,
                ICommandHandler<RemoveCookPhotoCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new RemoveCookPhotoCommand(entryId, context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("removeCookPhotoV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Remove your photo of an attempt")
            .WithDescription(
                "The entry itself stays: that you cooked it is still true. The stored file is left "
                + "alone, because it is content-addressed and another attempt may be the same "
                + "picture; reclaiming it belongs to a sweep, not to a delete.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}

/// <summary>Serves the picture of one attempt.</summary>
internal sealed class GetCookPhotoEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/recipes/{{recipeId:guid}}/cook-log/{{entryId:guid}}/photo", async (
                Guid entryId,
                HttpContext context,
                IQueryHandler<GetCookPhotoQuery, ImageDelivery> handler,
                CancellationToken cancellationToken) =>
            {
                if (!ImageResponse.TryReadWidth(context, out var width))
                {
                    return CustomResults.Problem(Domain.Recipes.ImageErrors.UnknownWidth);
                }

                var buffer = new MemoryStream();

                await using (buffer.ConfigureAwait(false))
                {
                    var result = await handler
                        .Handle(
                            new GetCookPhotoQuery(
                                entryId,
                                context.CurrentUser().UserId,
                                width,
                                buffer),
                            cancellationToken)
                        .ConfigureAwait(false);

                    return result.Match(
                        delivery => ImageResponse.Served(context, buffer, delivery),
                        CustomResults.Problem);
                }
            })
            .WithName("getCookPhotoV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Read your photo of an attempt")
            .WithDescription(
                "Widths 400, 800 and 1600. Private and revalidated, because a photograph is "
                + "replaced under the address it was served from; its ETag is the content hash, "
                + "so a picture that was replaced is fetched and one that was not answers 304.")
            .WithRepeatableQueryParameters(["w"], [], ["w"])
            .Produces<byte[]>(StatusCodes.Status200OK, "image/webp")
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }


}
