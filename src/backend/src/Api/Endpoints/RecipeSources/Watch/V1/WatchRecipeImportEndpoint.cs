using System.Globalization;
using System.Net.ServerSentEvents;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.Sources;
using Contracts.Recipes.Sources;

namespace Api.Endpoints.RecipeSources.Watch.V1;

/// <summary>Streams an import's outcomes as they land.</summary>
internal sealed class WatchRecipeImportEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(
                $"{ApiPaths.V1}/recipe-sources/{{sourceId:guid}}/imports/{{importId:guid}}/events",
                async (
                    Guid sourceId,
                    Guid importId,
                    HttpContext context,
                    IQueryHandler<WatchImportQuery, ImportProgress> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler
                        .Handle(
                            new WatchImportQuery(
                                sourceId,
                                importId,
                                context.CurrentUser().UserId,
                                Resumed(context)),
                            cancellationToken)
                        .ConfigureAwait(false);

                    return result.Match(Stream, CustomResults.Problem);
                })
            .WithName("watchRecipeImportV1")
            .WithTags(Tags.RecipeSources)
            .WithSummary("Follow an import that is running")
            .WithDescription(
                "Server-sent events: one per recipe finished, in the order they finished, and a "
                + "last one with no recipe on it saying the run is over.\n\n"
                + "It replays before it waits. Every event carries an id, and a reconnect that "
                + "sends `Last-Event-ID` picks up from exactly there — a connection that dropped "
                + "in the middle loses nothing and repeats nothing. A stream opened for the first "
                + "time is the same thing with nothing to skip, which is what makes following an "
                + "import from another device work.\n\n"
                + "A silent stream sends an event with no recipe on it every fifteen seconds. A "
                + "recipe can take a while, and a connection that says nothing for minutes is one "
                + "a proxy will close.\n\n"
                + "Only the person who started an import may follow it, and a run is forgotten "
                + "half an hour after it ends — both are 404, because neither is worth telling a "
                + "stranger apart.")
            .Produces<ImportEvent>(StatusCodes.Status200OK, "text/event-stream")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }

    private static IResult Stream(ImportProgress progress) =>
        TypedResults.ServerSentEvents(Numbered(progress.Events));

    /// <summary>
    /// Numbers each event, so a reconnect can say where it got to.
    /// </summary>
    /// <remarks>
    /// The id is how many outcomes the stream has sent, which is exactly what
    /// the handler wants back to resume — no table of offsets, and no meaning
    /// to keep in step between the two ends.
    /// </remarks>
    private static async IAsyncEnumerable<SseItem<ImportEvent>> Numbered(
        IAsyncEnumerable<ImportEvent> events)
    {
        await foreach (var one in events.ConfigureAwait(false))
        {
            yield return new SseItem<ImportEvent>(one)
            {
                EventId = one.Done.ToString(CultureInfo.InvariantCulture)
            };
        }
    }

    /// <summary>How many outcomes the caller already has, from its reconnect.</summary>
    private static int Resumed(HttpContext context) =>
        int.TryParse(
            context.Request.Headers["Last-Event-ID"],
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var sent)
            ? sent
            : 0;
}
