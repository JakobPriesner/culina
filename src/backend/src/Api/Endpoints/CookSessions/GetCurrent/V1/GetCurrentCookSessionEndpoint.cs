using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.CookSessions;
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

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getCurrentCookSessionV1")
            .WithTags(Tags.CookSessions)
            .WithSummary("What you are cooking")
            .WithDescription(
                "404 when nothing is being cooked. Drives the bar that lets you pick a recipe "
                + "back up where you left it, so the answer carries the recipe's title and "
                + "needs no second request.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
