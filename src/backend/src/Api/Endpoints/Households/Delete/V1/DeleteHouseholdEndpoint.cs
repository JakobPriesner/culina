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
                var expected = ETag.RequireIfMatch(context);

                return await expected.Match(
                    async version =>
                    {
                        var result = await handler
                            .Handle(
                                new DeleteHouseholdCommand(householdId, context.CurrentUser().UserId, version),
                                cancellationToken)
                            .ConfigureAwait(false);

                        return result.Match(Results.NoContent, CustomResults.Problem);
                    },
                    error => Task.FromResult(CustomResults.Problem(error))).ConfigureAwait(false);
            })
            .WithName("deleteHouseholdV1")
            .WithTags(Tags.Households)
            .WithSummary("Delete a household")
            .WithDescription(
                "Owners only. Deletes the household and every recipe, tag and shopping list it "
                + "owns. There is no undo, which is why `If-Match` is required — missing is 428, "
                + "stale is 412.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .RequireAuthorization();
    }
}
