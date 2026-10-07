using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Households.GetInvitationByCode;
using Response = Contracts.Households.GetInvitationByCode.Response;

namespace Api.Endpoints.Invitations.GetByCode.V1;

/// <summary>Names the household an invitation code admits to, without using it.</summary>
/// <remarks>
/// Signed in only, like the code's whole purpose: the name is what somebody
/// deciding whether to press Join needs, and a signed-out holder of a link has
/// no decision to make yet.
/// </remarks>
internal sealed class GetInvitationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/invitations/{{code}}", async (
                string code,
                IQueryHandler<GetInvitationByCodeQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetInvitationByCodeQuery(code), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getInvitationByCodeV1")
            .WithTags(Tags.Households)
            .WithSummary("Read an invitation")
            .WithDescription(
                "The name of the household a code admits to, so the join page can show it before "
                + "anybody presses Join. Nothing is used up. Unknown, expired and already-used codes "
                + "all return the identical households.invitation_invalid, as redeeming does.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitExtensions.InvitationLookup);
    }
}
