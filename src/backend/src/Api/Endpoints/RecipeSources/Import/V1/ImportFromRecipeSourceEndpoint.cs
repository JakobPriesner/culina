using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Recipes.Sources;
using Contracts.Recipes.Sources;

namespace Api.Endpoints.RecipeSources.Import.V1;

/// <summary>Starts bringing a selection of recipes over.</summary>
internal sealed class ImportFromRecipeSourceEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/recipe-sources/{{sourceId:guid}}/imports", async (
                Guid sourceId,
                ImportFromSourceRequest request,
                HttpContext context,
                ICommandHandler<ImportFromSourceCommand, ImportStartedResponse> handler,
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

                return result.Match(
                    started => Results.Accepted(
                        $"{ApiPaths.V1}/recipe-sources/{sourceId}/imports/{started.ImportId}/events",
                        started),
                    CustomResults.Problem);
            })
            .WithName("importFromRecipeSourceV1")
            .WithTags(Tags.RecipeSources)
            .WithSummary("Bring recipes over from a connected library")
            .WithDescription(
                "Names the whole selection, and comes back as soon as the import has an id and a "
                + "shelf — usually in milliseconds. The recipes arrive afterwards, brought over by "
                + "a background worker a few at a time. Follow it at "
                + "`/recipe-sources/{sourceId}/imports/{importId}/events`, which is where every "
                + "outcome is reported.\n\n"
                + "The import belongs to the server, not to the tab that asked for it. Nobody has "
                + "to stay and watch: a closed laptop costs the progress display and nothing "
                + "else.\n\n"
                + "Idempotent by construction: a recipe already brought into this household comes "
                + "back as `already_here` rather than a second copy or an error. That is also how "
                + "somebody catches up on what is new a month later, and why an import that was "
                + "interrupted can simply be asked for again.\n\n"
                + "Everything from one import lands on a cookbook named after where it came from "
                + "and when, and this answer says which — so the way out of the screen exists "
                + "before the first recipe does. The shelf is what makes an import something you "
                + "can look at, check, and throw away.\n\n"
                + "A recipe's photo comes with it when it can be had: fetched from the connected "
                + "server only, never from an address that server merely names, and put through "
                + "the same decode-and-re-encode an upload gets. It is best effort — a picture "
                + "that is missing, slow, too large or not a picture leaves a recipe that is "
                + "complete in every other way, exactly like one somebody typed without a "
                + "photo.\n\n"
                + "One recipe failing never undoes the others: each is written in its own "
                + "transaction, and reported on its own line.")
            .RequireRateLimiting(RateLimitExtensions.Source)
            .Produces<ImportStartedResponse>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireAuthorization();
    }
}
