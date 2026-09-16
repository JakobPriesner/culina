using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Cookbooks;
using Domain.Cookbooks;

namespace Api.Endpoints.Cookbooks.Delete.V1;

/// <summary>Removes a cookbook.</summary>
internal sealed class DeleteCookbookEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/cookbooks/{{cookbookId:guid}}", async (
                Guid cookbookId,
                HttpContext context,
                ICommandHandler<DeleteCookbookCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new DeleteCookbookCommand(cookbookId, context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    Results.NoContent,
                    // Deleting something already gone is the outcome the caller
                    // wanted, so a second DELETE answers 204 rather than 404.
                    error => error.Code == CookbookErrors.NotFound(cookbookId).Code
                        ? Results.NoContent()
                        : CustomResults.Problem(error));
            })
            .WithName("deleteCookbookV1")
            .WithTags(Tags.Cookbooks)
            .WithSummary("Delete a cookbook")
            .WithDescription(
                "The shelf only. Every recipe that was on it stays exactly where it was — a "
                + "cookbook is a pointer, and deleting one deletes no food. Idempotent: deleting "
                + "one that is already gone also answers 204.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }
}
