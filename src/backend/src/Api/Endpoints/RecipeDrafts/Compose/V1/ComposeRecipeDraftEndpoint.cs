using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.Drafts;
using Event = Contracts.Recipes.Drafts.Event;
using Request = Contracts.Recipes.Drafts.Request;

namespace Api.Endpoints.RecipeDrafts.Compose.V1;

/// <summary>Asks the assistant for a recipe.</summary>
internal sealed class ComposeRecipeDraftEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/recipe-drafts", async (
                Request request,
                HttpContext context,
                ICommandHandler<ComposeRecipeDraftCommand, DraftProgress> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new ComposeRecipeDraftCommand(
                            request.Kind,
                            request.HouseholdId,
                            request.Material,
                            request.RecipeId,
                            request.Language,
                            context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Stream, CustomResults.Problem);
            })
            .WithName("composeRecipeDraftV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Ask the assistant for a recipe")
            .WithDescription(
                "A draft, never a recipe. Nothing is created: what comes back is shown for "
                + "correction and accepted a field at a time through the ordinary recipe "
                + "endpoints.\n\n"
                + "`kind` says which of three: `idea` turns a sentence about dinner into a "
                + "draft, `text` reads one out of something pasted, and `revision` rewrites "
                + "the recipe named by `recipeId` — keeping its ingredients, its amounts and "
                + "the language it is written in, and changing only how it reads. `language` "
                + "is ignored for a revision.\n\n"
                + "Server-sent events, because a model writes a recipe over tens of seconds "
                + "and a screen that shows it arriving is a screen somebody reads rather than "
                + "waits at. Each event carries the whole draft as far as it has been "
                + "written — a title, then ingredients, then steps — and the last one says "
                + "`finished`. A field the model has not finished writing is absent rather "
                + "than half-written.\n\n"
                + "Everything that can refuse the ask is decided before the stream opens, so "
                + "it is still an ordinary status code: 404 when this instance has no "
                + "assistant, or has that capability switched off — the two are one answer "
                + "because a caller learns nothing from being told which — and 429 when the "
                + "month's budget is spent. What goes wrong afterwards arrives as `problem` "
                + "on the last event, because by then the 200 has been sent.\n\n"
                + "The answer is checked on the way out: a line the app could not store loses "
                + "the part it could not store rather than failing the whole draft, because a "
                + "draft is a thing somebody is about to correct anyway.")
            .Produces<Event>(StatusCodes.Status200OK, "text/event-stream")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireRateLimiting(RateLimitExtensions.Assistance)
            .RequireAuthorization();
    }

    private static IResult Stream(DraftProgress progress) =>
        TypedResults.ServerSentEvents(progress.Events);
}
