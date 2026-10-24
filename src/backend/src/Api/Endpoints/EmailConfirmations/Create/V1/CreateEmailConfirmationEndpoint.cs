using Api.Authentication;
using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.EmailConfirmations.Create;
using Application.Sessions.SignIn;
using Request = Contracts.EmailConfirmations.Create.Request;
using Response = Contracts.Sessions.SignIn.Response;

namespace Api.Endpoints.EmailConfirmations.Create.V1;

/// <summary>Confirms an address with the mailed link.</summary>
internal sealed class CreateEmailConfirmationEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/email-confirmations", async (
                Request request,
                HttpContext context,
                ICommandHandler<ConfirmEmailCommand, SignInOutcome> handler,
                CookieSettings cookies,
                TimeProvider time,
                CancellationToken cancellationToken) =>
            {
                var command = new ConfirmEmailCommand(
                    request.Token,
                    context.ClientAddress(),
                    context.Request.Headers.UserAgent.ToString(),
                    SessionCookies.Read(context, cookies));

                var result = await handler.Handle(command, cancellationToken).ConfigureAwait(false);

                return result.Match(
                    outcome => SignInLinks.LinkSessionResults.Created(context, cookies, time, outcome),
                    CustomResults.Problem);
            })
            .WithName("createEmailConfirmationV1")
            .WithTags(Tags.EmailConfirmations)
            .WithSummary("Confirm an email address")
            .WithDescription(
                "Uses up the token from a confirmation link, records the address as confirmed and starts a browser "
                + "session, as signInV1 does. Failure is always auth.invalid_link.")
            .Produces<Response>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous()
            .WithMetadata(new CsrfExempt())
            .RequireRateLimiting(RateLimitExtensions.Login);
    }
}
