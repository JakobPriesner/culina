using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Households.Delete;

namespace Api.Endpoints.Households.Delete.V1;

/// <summary>Deletes a household.</summary>
internal sealed class DeleteHouseholdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/households/{{householdId:guid}}", async (
                Guid householdId,
                HttpContext context,
                ICommandHandler<DeleteHouseholdCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new DeleteHouseholdCommand(householdId, context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("deleteHouseholdV1")
            .WithTags(Tags.Households)
            .WithSummary("Delete a household")
            .WithDescription(
                "Owners only. Deletes the household and every recipe, tag and shopping list it "
                + "owns. There is no undo.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
