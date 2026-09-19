using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Recipes.Drafts;
using Domain.Assistance;
using Response = Contracts.Recipes.Drafts.Response;

namespace Api.Endpoints.RecipeDrafts.FromPhotograph.V1;

/// <summary>Reads a recipe out of a photograph.</summary>
/// <remarks>
/// Its own route rather than a third shape of <c>POST /recipe-drafts</c>,
/// because a photograph arrives as multipart and the other three arrive as
/// JSON — and one route cannot bind both. <c>photographs</c> is the sub-
/// collection the photograph is posted to, which keeps it a noun; the draft
/// that comes back is the same one the JSON route returns.
/// </remarks>
internal sealed class ReadRecipeDraftEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/recipe-drafts/photographs", async (
                IFormFile file,
                Guid householdId,
                string? language,
                HttpContext context,
                StorageSettings storage,
                ICommandHandler<ComposeRecipeDraftCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                if (file.Length > storage.MaxImageBytes)
                {
                    return CustomResults.Problem(AssistanceErrors.TooMuchToWorkFrom);
                }

                var photograph = await ReadAsync(file, cancellationToken).ConfigureAwait(false);

                var result = await handler
                    .Handle(
                        new ComposeRecipeDraftCommand(
                            "photo",
                            householdId,
                            Material: null,
                            RecipeId: null,
                            language,
                            context.CurrentUser().UserId)
                        {
                            Photograph = photograph,
                            PhotographMediaType = file.ContentType
                        },
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("readRecipeDraftV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Read a recipe out of a photograph")
            .WithDescription(
                "A cookbook page, a card, a screenshot. What comes back is the same draft the "
                + "other kinds return — nothing is created, and it is shown for correction.\n\n"
                + "The assistant is told to transcribe rather than improve, and to leave a gap "
                + "where the source is unreadable rather than guessing: a plausible number "
                + "invented for a blurred corner is the one failure of this capability somebody "
                + "would not catch.\n\n"
                + "404 when this instance has no assistant or the capability is off. 429 when "
                + "the month's budget is spent.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .WithQueryParameters("householdId", "language")
            .RequireRateLimiting(RateLimitExtensions.Assistance)
            .DisableAntiforgery()
            .RequireAuthorization();
    }

    /// <summary>
    /// The whole file, in memory.
    /// </summary>
    /// <remarks>
    /// Bounded by the same ceiling an upload has, checked before this runs. A
    /// photograph goes into a request body that the client may build more than
    /// once, so it has to be bytes rather than a stream.
    /// </remarks>
    private static async Task<ReadOnlyMemory<byte>> ReadAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var content = file.OpenReadStream();

        await using (content.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();

            await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

            return buffer.ToArray();
        }
    }
}
