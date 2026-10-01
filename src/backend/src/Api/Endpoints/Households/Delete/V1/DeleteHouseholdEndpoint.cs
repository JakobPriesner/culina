using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Households.Delete;
using Domain.Households;

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

                        return result.Match(
                            Results.NoContent,
                            // Already in the bin, never there, or not the
                            // caller's: one answer for all three, as a recipe
                            // gives. A second tap on Delete is the outcome the
                            // caller wanted, and a stranger learns nothing.
                            error => error.Code == HouseholdErrors.NotFound(householdId).Code
                                ? Results.NoContent()
                                : CustomResults.Problem(error));
                    },
                    error => Task.FromResult(CustomResults.Problem(error))).ConfigureAwait(false);
            })
            .WithName("deleteHouseholdV1")
            .WithTags(Tags.Households)
            .WithSummary("Delete a household")
            .WithDescription(
                "Owners only; a plain member gets 403. Puts the household in the bin for 30 days: "
                + "every member loses access at once, and its recipes and cookbooks are hidden "
                + "with it until an owner restores it. `If-Match` is required — missing is 428, "
                + "stale is 412. Idempotent: a household already deleted, or one the caller is not "
                + "in, also answers 204.")
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
