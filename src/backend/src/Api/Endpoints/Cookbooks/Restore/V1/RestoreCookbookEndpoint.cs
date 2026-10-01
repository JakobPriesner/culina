using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Cookbooks;

namespace Api.Endpoints.Cookbooks.Restore.V1;

/// <summary>Takes a cookbook out of its household's bin.</summary>
internal sealed class RestoreCookbookEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/cookbooks/{{cookbookId:guid}}/restorations", async (
                Guid cookbookId,
                HttpContext context,
                ICommandHandler<RestoreCookbookCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new RestoreCookbookCommand(cookbookId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("restoreCookbookV1")
            .WithTags(Tags.Cookbooks)
            .WithSummary("Restore a deleted cookbook")
            .WithDescription(
                "Any member of the cookbook's household may, until it is purged; it comes back with the "
                + "recipes it held. 404 when it is not in a bin the caller can open.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
