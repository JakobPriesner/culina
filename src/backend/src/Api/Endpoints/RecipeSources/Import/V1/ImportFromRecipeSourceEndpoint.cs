using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.Sources;
using Contracts.Recipes.Sources;

namespace Api.Endpoints.RecipeSources.Import.V1;

/// <summary>Brings a batch of recipes over.</summary>
internal sealed class ImportFromRecipeSourceEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/recipe-sources/{{sourceId:guid}}/imports", async (
                Guid sourceId,
                ImportFromSourceRequest request,
                HttpContext context,
                ICommandHandler<ImportFromSourceCommand, ImportFromSourceResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new ImportFromSourceCommand(
                            sourceId,
                            context.CurrentUser().UserId,
                            request),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("importFromRecipeSourceV1")
            .WithTags(Tags.RecipeSources)
            .WithSummary("Bring recipes over from a connected library")
            .WithDescription(
                "A batch of at most 25, not a whole library. The caller walks its own selection a "
                + "batch at a time, which is what makes an import of eight hundred recipes show "
                + "honest progress and survive a closed laptop — every batch is a complete "
                + "request, and asking twice is asking once.\n\n"
                + "Idempotent by construction: a recipe already brought into this household comes "
                + "back as `already_here` rather than a second copy or an error. That is also how "
                + "somebody catches up on what is new a month later.\n\n"
                + "Everything from one import lands on a cookbook named after where it came from "
                + "and when. Pass that `cookbookId` back on every batch after the first, so a "
                + "selection imported in twenty requests is one shelf rather than twenty. The "
                + "shelf is what makes an import something you can look at, check, and throw "
                + "away.\n\n"
                + "A recipe's photo comes with it when it can be had: fetched from the connected "
                + "server only, never from an address that server merely names, and put through "
                + "the same decode-and-re-encode an upload gets. It is best effort — a picture "
                + "that is missing, slow, too large or not a picture leaves a recipe that is "
                + "complete in every other way, exactly like one somebody typed without a "
                + "photo.\n\n"
                + "One line comes back per recipe asked for — `imported`, `already_here` or "
                + "`failed` — so the twelve that could not be read can be shown by name. One "
                + "recipe failing never undoes the others: each is written in its own "
                + "transaction.")
            .RequireRateLimiting(RateLimitExtensions.Import)
            .Produces<ImportFromSourceResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireAuthorization();
    }
}
