using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Sessions.GetAll;
using Response = Contracts.Sessions.GetAll.Response;

namespace Api.Endpoints.Sessions.GetAll.V1;

/// <summary>Lists the caller's signed-in devices.</summary>
internal sealed class GetSessionsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/sessions", async (
                HttpContext context,
                IQueryHandler<GetSessionsQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var user = context.CurrentUser();

                var result = await handler
                    .Handle(new GetSessionsQuery(user.UserId, user.SessionId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getSessionsV1")
            .WithTags(Tags.Sessions)
            .WithSummary("List your signed-in devices")
            .WithDescription("Every active session for the caller, most recently used first.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }
}
