using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.SignInLinks.Create;
using Request = Contracts.SignInLinks.Create.Request;

namespace Api.Endpoints.SignInLinks.Create.V1;

/// <summary>Mails a link that signs in without a password.</summary>
internal sealed class CreateSignInLinkEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/sign-in-links", async (
                Request request,
                ICommandHandler<RequestSignInLinkCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new RequestSignInLinkCommand(request.Email), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(() => Results.Accepted(), CustomResults.Problem);
            })
            .WithName("createSignInLinkV1")
            .WithTags(Tags.SignInLinks)
            .WithSummary("Mail a sign-in link")
            .WithDescription(
                "Answers 202 whether or not the address is registered; when it is, a link that signs in once, "
                + "within 15 minutes, is mailed to it (redeemSignInLinkV1). 409 mail.off when the instance sends no mail.")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous()
            .WithMetadata(new CsrfExempt())
            .RequireRateLimiting(RateLimitExtensions.Login);
    }
}
