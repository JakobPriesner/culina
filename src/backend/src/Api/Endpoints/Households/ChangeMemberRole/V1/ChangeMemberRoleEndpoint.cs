using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Households.ChangeMemberRole;
using Request = Contracts.Households.ChangeMemberRole.Request;
using Response = Contracts.Households.ChangeMemberRole.Response;

namespace Api.Endpoints.Households.ChangeMemberRole.V1;

/// <summary>Changes what a member may do.</summary>
internal sealed class ChangeMemberRoleEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPatch($"{ApiPaths.V1}/households/{{householdId:guid}}/members/{{userId:guid}}", async (
                Guid householdId,
                Guid userId,
                Request request,
                HttpContext context,
                ICommandHandler<ChangeMemberRoleCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new ChangeMemberRoleCommand(
                            householdId,
                            userId,
                            context.CurrentUser().UserId,
                            request.Role),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("changeHouseholdMemberRoleV1")
            .WithTags(Tags.Households)
            .WithSummary("Change a member's role")
            .WithDescription(
                "Owners only. Demoting the last owner is refused: a household with no owner can "
                + "never be administered again.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();
    }
}
