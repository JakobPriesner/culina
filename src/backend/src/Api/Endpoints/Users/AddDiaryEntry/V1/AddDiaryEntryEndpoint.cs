using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Users.AddDiaryEntry;
using Request = Contracts.Users.AddDiaryEntry.Request;
using Response = Contracts.Users.AddDiaryEntry.Response;

namespace Api.Endpoints.Users.AddDiaryEntry.V1;

/// <summary>Logs something you ate.</summary>
internal sealed class AddDiaryEntryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/users/me/diary/entries", async (
                Request request,
                HttpContext context,
                ICommandHandler<AddDiaryEntryCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(request.ToCommand(context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    added => Results.Created($"{ApiPaths.V1}/users/me/diary/entries/{added.Entry.EntryId}", added),
                    CustomResults.Problem);
            })
            .WithName("addDiaryEntryV1")
            .WithTags(Tags.Users)
            .WithSummary("Log something you ate")
            .WithDescription(
                "A recipe in portions, a food in grams or a typical unit, or your own typed figures. The server "
                + "looks up and keeps the figures, marks a lower bound (atLeast) or an estimate, and never takes "
                + "figures from the client for a recipe or a food.")
            .Produces<Response>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
