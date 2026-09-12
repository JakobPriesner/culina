using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Households.RemoveMember;

namespace Api.Endpoints.Households.RemoveMember.V1;

/// <summary>Removes someone from a household, or leaves it.</summary>
internal sealed class RemoveMemberEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/households/{{householdId:guid}}/members/{{userId:guid}}", async (
                Guid householdId,
                Guid userId,
                HttpContext context,
                ICommandHandler<RemoveMemberCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new RemoveMemberCommand(householdId, userId, context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("removeHouseholdMemberV1")
            .WithTags(Tags.Households)
            .WithSummary("Remove a member, or leave")
            .WithDescription(
                "An owner may remove anyone; anyone may remove themselves. Neither may leave the "
                + "household without an owner.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();
    }
}
