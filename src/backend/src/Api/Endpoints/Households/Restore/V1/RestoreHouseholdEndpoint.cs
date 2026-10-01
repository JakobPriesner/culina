using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Households.Restore;

namespace Api.Endpoints.Households.Restore.V1;

/// <summary>Takes a household out of the bin.</summary>
internal sealed class RestoreHouseholdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/households/{{householdId:guid}}/restorations", async (
                Guid householdId,
                HttpContext context,
                ICommandHandler<RestoreHouseholdCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new RestoreHouseholdCommand(householdId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("restoreHouseholdV1")
            .WithTags(Tags.Households)
            .WithSummary("Restore a deleted household")
            .WithDescription(
                "Owners only, until it is purged. It comes back with its members, recipes and cookbooks; "
                + "anything deleted inside it before it was deleted stays in its bin.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
