using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Searches;
using Domain.Searches;

namespace Api.Endpoints.Searches.Delete.V1;

/// <summary>Forgets a saved search.</summary>
internal sealed class DeleteSavedSearchEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/searches/{{searchId:guid}}", async (
                Guid searchId,
                HttpContext context,
                ICommandHandler<DeleteSavedSearchCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new DeleteSavedSearchCommand(searchId, context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    Results.NoContent,
                    // Deleting something already gone is the outcome the caller
                    // wanted, so a second DELETE answers 204 rather than 404.
                    error => error.Code == SavedSearchErrors.NotFound(searchId).Code
                        ? Results.NoContent()
                        : CustomResults.Problem(error));
            })
            .WithName("deleteSavedSearchV1")
            .WithTags(Tags.Searches)
            .WithSummary("Forget a saved search")
            .WithDescription(
                "The name and the filters only. Nothing it found is touched — a saved search is a "
                + "question, and forgetting one deletes no food. Idempotent: deleting one that is "
                + "already gone also answers 204.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }
}
