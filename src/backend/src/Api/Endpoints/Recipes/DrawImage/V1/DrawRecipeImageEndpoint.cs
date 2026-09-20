using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.DrawImage;
using Contracts.Recipes;

namespace Api.Endpoints.Recipes.DrawImage.V1;

/// <summary>Draws a recipe's image.</summary>
internal sealed class DrawRecipeImageEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/recipes/{{recipeId:guid}}/image", async (
                Guid recipeId,
                HttpContext context,
                ICommandHandler<DrawRecipeImageCommand, DrawingProgress> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new DrawRecipeImageCommand(recipeId, context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Stream, CustomResults.Problem);
            })
            .WithName("drawRecipeImageV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Draw a recipe image")
            .WithDescription(
                "POST and PUT mean different things on this one sub-resource, and the "
                + "difference is who made the picture. PUT replaces it with bytes you are "
                + "sending; POST asks the assistant to make one, so it carries no body.\n\n"
                + "What comes back goes through exactly the same path an upload does — decoded "
                + "to find out what it is, re-encoded to WebP at three widths — so a drawn "
                + "image is an ordinary recipe photo in every respect afterwards.\n\n"
                + "Server-sent events, because this is the slowest call in the app: sixteen "
                + "seconds is a fast drawing and two minutes is the ceiling. A picture has no "
                + "halfway state to send, so the events carry the elapsed seconds and nothing "
                + "else until the last one, which carries the recipe with its new picture or "
                + "a `problem` saying why there is none. A request that said nothing for two "
                + "minutes is one a proxy closes and a person gives up on — while the server "
                + "finishes the drawing, pays for it, and stores it.\n\n"
                + "404 when this instance has no assistant or drawing is switched off. 400 when "
                + "the connected provider cannot draw at all, which is the case for a model "
                + "running on your own hardware. 429 when the month's budget is spent. Those "
                + "are decided before the stream opens; anything later is on the last event.")
            .Produces<DrawingEvent>(StatusCodes.Status200OK, "text/event-stream")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireRateLimiting(RateLimitExtensions.Assistance)
            .RequireAuthorization();
    }

    private static IResult Stream(DrawingProgress progress) =>
        TypedResults.ServerSentEvents(progress.Events);
}
