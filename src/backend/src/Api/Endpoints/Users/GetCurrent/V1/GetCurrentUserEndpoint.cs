using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Users.GetCurrent;
using Response = Contracts.Users.GetCurrent.Response;

namespace Api.Endpoints.Users.GetCurrent.V1;

/// <summary>Reads the signed-in user.</summary>
internal sealed class GetCurrentUserEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/users/me", async (
                HttpContext context,
                IQueryHandler<GetCurrentUserQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetCurrentUserQuery(context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(user => ETag.Ok(context, user, user.Version), CustomResults.Problem);
            })
            .WithName("getCurrentUserV1")
            .WithTags(Tags.Users)
            .WithSummary("Read your account")
            .WithDescription(
                "The signed-in user and the households they belong to. The client calls this on "
                + "boot to resolve the session, so memberships are included rather than requiring "
                + "a second request on the critical path.")
            .Produces<Response>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }
}
