using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Users.UpdateDiaryEntry;
using Request = Contracts.Users.UpdateDiaryEntry.Request;
using Response = Contracts.Users.UpdateDiaryEntry.Response;

namespace Api.Endpoints.Users.UpdateDiaryEntry.V1;

/// <summary>Moves a diary entry, or changes how much it was.</summary>
internal sealed class UpdateDiaryEntryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPatch($"{ApiPaths.V1}/users/me/diary/entries/{{entryId:guid}}", async (
                Guid entryId,
                Request request,
                HttpContext context,
                ICommandHandler<UpdateDiaryEntryCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(request.ToCommand(context.CurrentUser().UserId, entryId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("updateDiaryEntryV1")
            .WithTags(Tags.Users)
            .WithSummary("Change a diary entry")
            .WithDescription(
                "A new day or meal moves the entry and keeps its figures; a new amount looks them up again. "
                + "Typed entries have no amount to change. No If-Match: one person's own diary.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
