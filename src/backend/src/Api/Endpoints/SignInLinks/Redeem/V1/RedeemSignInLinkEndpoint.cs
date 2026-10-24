using Api.Authentication;
using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Sessions.SignIn;
using Application.SignInLinks.Redeem;
using Request = Contracts.SignInLinks.Redeem.Request;
using Response = Contracts.Sessions.SignIn.Response;

namespace Api.Endpoints.SignInLinks.Redeem.V1;

/// <summary>Signs a browser in with a mailed sign-in link.</summary>
internal sealed class RedeemSignInLinkEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/sign-in-links/redemptions", async (
                Request request,
                HttpContext context,
                ICommandHandler<RedeemSignInLinkCommand, SignInOutcome> handler,
                CookieSettings cookies,
                TimeProvider time,
                CancellationToken cancellationToken) =>
            {
                var command = new RedeemSignInLinkCommand(
                    request.Token,
                    context.ClientAddress(),
                    context.Request.Headers.UserAgent.ToString(),
                    SessionCookies.Read(context, cookies));

                var result = await handler.Handle(command, cancellationToken).ConfigureAwait(false);

                return result.Match(
                    outcome => SignInLinks.LinkSessionResults.Created(context, cookies, time, outcome),
                    CustomResults.Problem);
            })
            .WithName("redeemSignInLinkV1")
            .WithTags(Tags.SignInLinks)
            .WithSummary("Sign in with a mailed link")
            .WithDescription(
                "Uses up the token from a sign-in link and starts a browser session, as signInV1 does; opening the "
                + "link also confirms the address. Failure is always auth.invalid_link.")
            .Produces<Response>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous()
            // Like signing in: the way in for a browser with no working session, and only a JSON body is read.
            .WithMetadata(new CsrfExempt())
            .RequireRateLimiting(RateLimitExtensions.Login);
    }
}
