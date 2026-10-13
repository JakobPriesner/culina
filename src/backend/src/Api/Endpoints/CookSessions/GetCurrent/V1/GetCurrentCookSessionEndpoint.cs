using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.CookSessions;
using Domain.Shared;
using Response = Contracts.CookSessions.Response;

namespace Api.Endpoints.CookSessions.GetCurrent.V1;

/// <summary>What the caller is cooking, if anything.</summary>
internal sealed class GetCurrentCookSessionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/cook-sessions/current", async (
                HttpContext context,
                IQueryHandler<GetCurrentCookSessionQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetCurrentCookSessionQuery(context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    Results.Ok,
                    // Nothing to resume is the usual answer, not a missing resource: no session,
                    // or one whose recipe the caller can no longer see.
                    error => error.Type == ErrorType.NotFound
                        ? Results.NoContent()
                        : CustomResults.Problem(error));
            })
            .WithName("getCurrentCookSessionV1")
            .WithTags(Tags.CookSessions)
            .WithSummary("What you are cooking")
            .WithDescription(
                "204 when nothing is being cooked. Drives the bar that lets you pick a recipe "
                + "back up where you left it, so the answer carries the recipe's title and "
                + "needs no second request.")
            .Produces<Response>()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }
}
