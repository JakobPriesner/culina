using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Households.GetInvitations;
using Response = Contracts.Households.GetInvitations.Response;

namespace Api.Endpoints.Households.GetInvitations.V1;

/// <summary>Lists a household's open invitations.</summary>
internal sealed class GetInvitationsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households/{{householdId:guid}}/invitations", async (
                Guid householdId,
                HttpContext context,
                IQueryHandler<GetInvitationsQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new GetInvitationsQuery(householdId, context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getHouseholdInvitationsV1")
            .WithTags(Tags.Households)
            .WithSummary("List open invitations")
            .WithDescription(
                "Owners only. Returns metadata and never a code: the code is shown once, at "
                + "creation, so this list cannot hand one out.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
