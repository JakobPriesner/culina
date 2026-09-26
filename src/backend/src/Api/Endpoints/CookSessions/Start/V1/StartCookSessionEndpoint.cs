using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.CookSessions;
using Request = Contracts.CookSessions.StartRequest;
using Response = Contracts.CookSessions.Response;

namespace Api.Endpoints.CookSessions.Start.V1;

/// <summary>Starts cooking a recipe.</summary>
internal sealed class StartCookSessionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/cook-sessions", async (
                Request request,
                HttpContext context,
                ICommandHandler<StartCookSessionCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new StartCookSessionCommand(
                            request.RecipeId,
                            context.CurrentUser().UserId,
                            request.Servings,
                            request.HouseholdId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    session => Results.Created($"{ApiPaths.V1}/cook-sessions/{session.SessionId}", session),
                    CustomResults.Problem);
            })
            .WithName("startCookSessionV1")
            .WithTags(Tags.CookSessions)
            .WithSummary("Start cooking")
            .WithDescription(
                "Starting a session gives up whatever else you had going, in one transaction. "
                + "At most one session is active per person, which is what makes "
                + "`GET /cook-sessions/current` a single unambiguous answer.")
            .Produces<Response>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
