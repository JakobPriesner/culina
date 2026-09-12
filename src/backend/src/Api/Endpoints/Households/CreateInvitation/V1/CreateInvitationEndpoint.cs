using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Households.CreateInvitation;
using Response = Contracts.Households.CreateInvitation.Response;

namespace Api.Endpoints.Households.CreateInvitation.V1;

/// <summary>Issues an invitation.</summary>
internal sealed class CreateInvitationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/households/{{householdId:guid}}/invitations", async (
                Guid householdId,
                HttpContext context,
                ICommandHandler<CreateInvitationCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new CreateInvitationCommand(householdId, context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    created => Results.Created(
                        $"{ApiPaths.V1}/households/{householdId}/invitations/{created.InvitationId}",
                        created),
                    CustomResults.Problem);
            })
            .WithName("createHouseholdInvitationV1")
            .WithTags(Tags.Households)
            .WithSummary("Invite someone")
            .WithDescription(
                "Owners only. The response carries the code, and it is the only time the code is "
                + "ever shown: only its digest is stored.")
            .Produces<Response>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
