using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Households.RemoveHeir;

namespace Api.Endpoints.Households.RemoveHeir.V1;

/// <summary>Stops a household inheriting this one's recipes.</summary>
internal sealed class RemoveHeirEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/households/{{householdId:guid}}/heirs/{{heirId:guid}}", async (
                Guid householdId,
                Guid heirId,
                HttpContext context,
                ICommandHandler<RemoveHeirCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new RemoveHeirCommand(householdId, heirId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("removeHouseholdHeirV1")
            .WithTags(Tags.Households)
            .WithSummary("Stop a household inheriting this one's recipes")
            .WithDescription(
                "Owners of this household only, and only a household that inherits from it directly; "
                + "any household inheriting through that one stops seeing the recipes too. 404 for a "
                + "household that does not inherit from this one.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .RequireAuthorization();
    }
}
