using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Recipes.Drafts;
using Domain.Assistance;
using Microsoft.AspNetCore.Mvc;
using Event = Contracts.Recipes.Drafts.Event;

namespace Api.Endpoints.RecipeDrafts.FromMedia.V1;

/// <summary>Reads a shared caption and up to eight screenshots as one recipe.</summary>
internal sealed class ReadMediaRecipeDraftEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/recipe-drafts/media", async (
                [FromForm] IFormFileCollection photos,
                [FromForm] string? material,
                [FromForm] string? transcript,
                Guid householdId,
                string? language,
                HttpContext context,
                StorageSettings storage,
                ICommandHandler<ComposeRecipeDraftCommand, DraftProgress> handler,
                CancellationToken cancellationToken) =>
            {
                if (photos.Count > 8 || photos.Sum(file => file.Length) > 40 * 1024 * 1024
                    || photos.Any(file => file.Length == 0 || file.Length > storage.MaxImageBytes))
                {
                    return CustomResults.Problem(AssistanceErrors.TooMuchToWorkFrom);
                }

                List<RecipePicture> pictures = [];
                foreach (var file in photos)
                {
                    // Match the bytes, not a caller's Content-Type. Only the formats
                    // the provider adapters understand are sent to a vision model.
                    using var source = file.OpenReadStream();
                    using var buffer = new MemoryStream();
                    await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
                    var bytes = buffer.ToArray();
                    var mediaType = MediaType(bytes);
                    if (mediaType is null)
                    {
                        return CustomResults.Problem(AssistanceErrors.NothingToWorkFrom);
                    }

                    pictures.Add(new RecipePicture(bytes, mediaType));
                }

                var result = await handler.Handle(
                    new ComposeRecipeDraftCommand(
                        "social", householdId, material, null, language, context.CurrentUser().UserId)
                    {
                        Transcript = transcript,
                        Pictures = pictures
                    }, cancellationToken).ConfigureAwait(false);

                return result.Match(progress => TypedResults.ServerSentEvents(progress.Events), CustomResults.Problem);
            })
            .WithName("readMediaRecipeDraftV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Read a shared recipe from captions and screenshots")
            .Produces<Event>(StatusCodes.Status200OK, "text/event-stream")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .WithQueryParameters("householdId", "language")
            .RequireRateLimiting(RateLimitExtensions.Assistance)
            .WithMetadata(new RequestSizeLimitAttribute(42 * 1024 * 1024))
            .DisableAntiforgery()
            .RequireAuthorization();
    }

    internal static string? MediaType(byte[] bytes) => bytes switch
    {
        [0xff, 0xd8, 0xff, ..] => "image/jpeg",
        [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, ..] => "image/png",
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => "image/webp",
        _ => null
    };
}
