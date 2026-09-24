using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Households.RedeemInvitation;
using Response = Contracts.Households.RedeemInvitation.Response;

namespace Api.Endpoints.Invitations.Redeem.V1;

/// <summary>Joins a household with an invitation code.</summary>
/// <remarks>
/// Redeeming <em>creates a redemption</em>, which is why this is a POST to a
/// sub-resource rather than a verb in the path.
/// </remarks>
internal sealed class RedeemInvitationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/invitations/{{code}}/redemptions", async (
                string code,
                HttpContext context,
                ICommandHandler<RedeemInvitationCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new RedeemInvitationCommand(code, context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    joined => joined.AlreadyMember
                        ? Results.Ok(joined)
                        : Results.Created($"{ApiPaths.V1}/households/{joined.HouseholdId}", joined),
                    CustomResults.Problem);
            })
            .WithName("redeemInvitationV1")
            .WithTags(Tags.Households)
            .WithSummary("Join a household")
            .WithDescription(
                "Unknown, expired and already-used codes all return the identical "
                + "households.invitation_invalid, so a code cannot be probed for validity. A "
                + "good code presented by somebody already in its household answers `200` with "
                + "`alreadyMember` and is not used up.")
            .Produces<Response>(StatusCodes.Status201Created)
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitExtensions.Invitation);
    }
}
