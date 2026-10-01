using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Users.CreateRecoveryCodes;
using Request = Contracts.Users.CreateRecoveryCodes.Request;
using Response = Contracts.Users.CreateRecoveryCodes.Response;

namespace Api.Endpoints.Users.CreateRecoveryCodes.V1;

/// <summary>Makes a new set of recovery codes for the signed-in user.</summary>
internal sealed class CreateRecoveryCodesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/users/me/recovery-codes", async (
                Request request,
                HttpContext context,
                ICommandHandler<CreateRecoveryCodesCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new CreateRecoveryCodesCommand(context.CurrentUser().UserId, request.Password),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    created => Results.Created($"{ApiPaths.V1}/users/me/recovery-codes", created),
                    CustomResults.Problem);
            })
            .WithName("createRecoveryCodesV1")
            .WithTags(Tags.Users)
            .WithSummary("Make recovery codes")
            .WithDescription(
                "Needs the password. Returns ten one-time codes, each able to set a new password "
                + "if this one is forgotten. They are shown once; any earlier set stops working.")
            .Produces<Response>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitExtensions.Login);
    }
}
