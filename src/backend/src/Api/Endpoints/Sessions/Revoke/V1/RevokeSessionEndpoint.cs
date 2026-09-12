using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Sessions.Revoke;

namespace Api.Endpoints.Sessions.Revoke.V1;

/// <summary>Ends one of the caller's other sessions.</summary>
internal sealed class RevokeSessionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/sessions/{{sessionId:guid}}", async (
                Guid sessionId,
                HttpContext context,
                ICommandHandler<RevokeSessionCommand> handler,
                CookieSettings cookies,
                CancellationToken cancellationToken) =>
            {
                var user = context.CurrentUser();

                var result = await handler
                    .Handle(new RevokeSessionCommand(sessionId, user.UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    () =>
                    {
                        // Revoking the session you are using is signing out, so
                        // the cookies go too rather than leaving the browser
                        // holding a dead credential.
                        if (sessionId == user.SessionId)
                        {
                            SessionCookies.Clear(context, cookies);
                        }

                        return Results.NoContent();
                    },
                    CustomResults.Problem);
            })
            .WithName("revokeSessionV1")
            .WithTags(Tags.Sessions)
            .WithSummary("Sign out one device")
            .WithDescription("Ends one of your sessions. Another user's session is never visible.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
