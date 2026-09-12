using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Sessions.Revoke;

namespace Api.Endpoints.Sessions.SignOut.V1;

/// <summary>Ends the session making the request.</summary>
internal sealed class SignOutEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/sessions/current", async (
                HttpContext context,
                ICommandHandler<RevokeSessionCommand> handler,
                CookieSettings cookies,
                CancellationToken cancellationToken) =>
            {
                var user = context.CurrentUser();

                var result = await handler
                    .Handle(new RevokeSessionCommand(user.SessionId, user.UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    () =>
                    {
                        // Server side first, then the cookie: a cleared cookie
                        // with a live session row would leave a credential
                        // valid for anyone who kept a copy of it.
                        SessionCookies.Clear(context, cookies);

                        return Results.NoContent();
                    },
                    CustomResults.Problem);
            })
            .WithName("signOutV1")
            .WithTags(Tags.Sessions)
            .WithSummary("Sign out")
            .WithDescription("Deletes the current session and clears its cookies.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }
}
