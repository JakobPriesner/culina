using Api.Authentication;
using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Sessions.SignIn;
using Request = Contracts.Sessions.SignIn.Request;
using Response = Contracts.Sessions.SignIn.Response;

namespace Api.Endpoints.Sessions.SignIn.V1;

/// <summary>Signs a user in.</summary>
internal sealed class SignInEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/sessions", async (
                Request request,
                HttpContext context,
                ICommandHandler<SignInCommand, SignInOutcome> handler,
                CookieSettings cookies,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(request.ToCommand(context), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    outcome =>
                    {
                        // The endpoint is the only place that sees both halves:
                        // the session token becomes an HttpOnly cookie and
                        // never appears in the body.
                        SessionCookies.Write(
                            context,
                            cookies,
                            outcome.SessionToken,
                            outcome.Response.CsrfToken);

                        return Results.Created($"{ApiPaths.V1}/sessions/current", outcome.Response);
                    },
                    CustomResults.Problem);
            })
            .WithName("signInV1")
            .WithTags(Tags.Sessions)
            .WithSummary("Sign in")
            .WithDescription(
                "Starts a session. Sets the session cookie and returns the CSRF token to echo in "
                + "X-Culina-CSRF on every unsafe request. Failure is always auth.invalid_credentials, "
                + "whether or not the address is registered.")
            .Produces<Response>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitExtensions.Login);
    }
}
