using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Archive;

namespace Api.Endpoints.Households.Archive.V1;

/// <summary>Writes a household's recipes out as a file.</summary>
internal sealed class ExportArchiveEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households/{{householdId:guid}}/archive", async (
                Guid householdId,
                HttpContext context,
                IQueryHandler<ExportArchiveQuery, ArchiveWritten> handler,
                CancellationToken cancellationToken) =>
            {
                // Named and typed before a byte is written, because once the
                // body has started there is no way back to a problem response.
                context.Response.ContentType = "application/json";
                context.Response.Headers.ContentDisposition =
                    $"attachment; filename=\"culina-{DateTime.UtcNow:yyyy-MM-dd}.json\"";

                var result = await handler
                    .Handle(
                        new ExportArchiveQuery(
                            householdId,
                            context.CurrentUser().UserId,
                            context.Response.Body),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(_ => Results.Empty, CustomResults.Problem);
            })
            .WithName("exportArchiveV1")
            .WithTags(Tags.Households)
            .WithSummary("Take your recipes with you")
            .WithDescription(
                "Plain, readable JSON: this household's recipes with their photographs inline, and "
                + "your own notes and cooking history. Notes are personal — two people in one "
                + "kitchen keep separate ones — so an archive carries the asking person's and "
                + "nobody else's.\n\n"
                + "A step's ingredient references travel as positions rather than ids, so a "
                + "restored step still names the right ingredient in whatever database it lands in.")
            .Produces<string>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}

/// <summary>Writes an archive's recipes into a household.</summary>
internal sealed class RestoreArchiveEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/households/{{householdId:guid}}/archive", async (
                Guid householdId,
                IFormFile file,
                HttpContext context,
                ICommandHandler<RestoreArchiveCommand, ArchiveRestored> handler,
                CancellationToken cancellationToken) =>
            {
                var content = file.OpenReadStream();

                await using (content.ConfigureAwait(false))
                {
                    var result = await handler
                        .Handle(
                            new RestoreArchiveCommand(
                                householdId,
                                context.CurrentUser().UserId,
                                content),
                            cancellationToken)
                        .ConfigureAwait(false);

                    return result.Match(Results.Ok, CustomResults.Problem);
                }
            })
            .WithName("restoreArchiveV1")
            .WithTags(Tags.Households)
            .WithSummary("Put an archive's recipes back")
            .WithDescription(
                "Adds the archive's recipes to this household; it never replaces what is already "
                + "there, because a restore that deleted your kitchen would be the worst possible "
                + "reading of the word.\n\n"
                + "One recipe at a time, in its own transaction. An archive is usually restored "
                + "because something went wrong, and refusing four hundred recipes over one that a "
                + "newer version wrote strangely is the least helpful thing it could do — what "
                + "could not be written is counted and reported.")
            .Produces<ArchiveRestored>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .DisableAntiforgery()
            .RequireAuthorization();
    }
}
