using Api.Authentication;
using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.PasswordResets.Create;
using Request = Contracts.PasswordResets.Create.Request;

namespace Api.Endpoints.PasswordResets.Create.V1;

/// <summary>Sets a new password with a recovery code.</summary>
internal sealed class CreatePasswordResetEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/password-resets", async (
                Request request,
                HttpContext context,
                ICommandHandler<ResetPasswordCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var command = new ResetPasswordCommand(
                    request.Email,
                    request.Code,
                    request.Password,
                    context.ClientAddress());

                var result = await handler
                    .Handle(command, cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("createPasswordResetV1")
            .WithTags(Tags.PasswordResets)
            .WithSummary("Set a new password with a recovery code")
            .WithDescription(
                "Uses up the code and signs the account out everywhere; sign in with the new password "
                + "afterwards. Failure is always auth.invalid_recovery_code, whether or not the address "
                + "is registered.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .AllowAnonymous()
            // Like signing in, this is a way back for somebody with no working
            // session, so a stale CSRF token must not stand in the way. The
            // code is the credential, and the endpoint reads only a JSON body,
            // which a cross-site form cannot send; see CsrfExempt.
            .WithMetadata(new CsrfExempt())
            .RequireRateLimiting(RateLimitExtensions.Login);
    }
}
