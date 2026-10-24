using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Users.RemoveDiaryEntry;

namespace Api.Endpoints.Users.RemoveDiaryEntry.V1;

/// <summary>Removes a diary entry.</summary>
internal sealed class RemoveDiaryEntryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/users/me/diary/entries/{{entryId:guid}}", async (
                Guid entryId,
                HttpContext context,
                ICommandHandler<RemoveDiaryEntryCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new RemoveDiaryEntryCommand(context.CurrentUser().UserId, entryId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("removeDiaryEntryV1")
            .WithTags(Tags.Users)
            .WithSummary("Remove a diary entry")
            .WithDescription("The undo in the client logs the entry again; nothing is kept here.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
