using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.PasswordResetLinks.Create;
using Request = Contracts.PasswordResetLinks.Create.Request;

namespace Api.Endpoints.PasswordResetLinks.Create.V1;

/// <summary>Mails a link that sets a new password.</summary>
internal sealed class CreatePasswordResetLinkEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/password-reset-links", async (
                Request request,
                ICommandHandler<RequestPasswordResetLinkCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new RequestPasswordResetLinkCommand(request.Email), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(() => Results.Accepted(), CustomResults.Problem);
            })
            .WithName("createPasswordResetLinkV1")
            .WithTags(Tags.PasswordResetLinks)
            .WithSummary("Mail a link to choose a new password")
            .WithDescription(
                "Answers 202 whether or not the address is registered; when it is, a link and a one-hour recovery "
                + "code are mailed to it, to use with createPasswordResetV1. 409 mail.off when the instance sends no "
                + "mail, the same for every address.")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous()
            // For somebody with no working session, like createPasswordResetV1; only a JSON body is read (see CsrfExempt).
            .WithMetadata(new CsrfExempt())
            .RequireRateLimiting(RateLimitExtensions.Login);
    }
}
