using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Households.RevokeInvitation;

namespace Api.Endpoints.Households.RevokeInvitation.V1;

/// <summary>Revokes an invitation.</summary>
internal sealed class RevokeInvitationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete(
                $"{ApiPaths.V1}/households/{{householdId:guid}}/invitations/{{invitationId:guid}}",
                async (
                    Guid householdId,
                    Guid invitationId,
                    HttpContext context,
                    ICommandHandler<RevokeInvitationCommand> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler
                        .Handle(
                            new RevokeInvitationCommand(
                                householdId,
                                invitationId,
                                context.CurrentUser().UserId),
                            cancellationToken)
                        .ConfigureAwait(false);

                    return result.Match(Results.NoContent, CustomResults.Problem);
                })
            .WithName("revokeHouseholdInvitationV1")
            .WithTags(Tags.Households)
            .WithSummary("Revoke an invitation")
            .WithDescription("Owners only. The code stops working immediately.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
