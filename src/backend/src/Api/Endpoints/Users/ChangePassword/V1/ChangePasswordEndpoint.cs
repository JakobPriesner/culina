using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Users.ChangePassword;
using Request = Contracts.Users.ChangePassword.Request;

namespace Api.Endpoints.Users.ChangePassword.V1;

/// <summary>Changes the signed-in user's password.</summary>
internal sealed class ChangePasswordEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/users/me/password", async (
                Request request,
                HttpContext context,
                ICommandHandler<ChangePasswordCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var user = context.CurrentUser();

                var result = await handler
                    .Handle(
                        new ChangePasswordCommand(
                            user.UserId,
                            user.SessionId,
                            request.CurrentPassword,
                            request.NewPassword),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("changePasswordV1")
            .WithTags(Tags.Users)
            .WithSummary("Change your password")
            .WithDescription(
                "Needs the current password. Every other session of the account is signed out; "
                + "the one making the change stays signed in.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitExtensions.Login);
    }
}
