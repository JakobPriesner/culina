using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Users.EraseDiary;

namespace Api.Endpoints.Users.EraseDiary.V1;

/// <summary>Deletes every entry of your diary.</summary>
internal sealed class EraseDiaryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/users/me/diary", async (
                HttpContext context,
                ICommandHandler<EraseDiaryCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new EraseDiaryCommand(context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("eraseDiaryV1")
            .WithTags(Tags.Users)
            .WithSummary("Delete your whole diary")
            .WithDescription(
                "Every entry, switched on or off; the settings stay. The client asks for a typed confirmation "
                + "first, the one place it does not offer an undo instead.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }
}
