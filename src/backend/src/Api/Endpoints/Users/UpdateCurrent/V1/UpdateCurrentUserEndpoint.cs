using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Users.UpdateCurrent;
using Request = Contracts.Users.UpdateCurrent.Request;
using Response = Contracts.Users.UpdateCurrent.Response;

namespace Api.Endpoints.Users.UpdateCurrent.V1;

/// <summary>Renames the signed-in user.</summary>
internal sealed class UpdateCurrentUserEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPatch($"{ApiPaths.V1}/users/me", async (
                Request request,
                HttpContext context,
                ICommandHandler<UpdateCurrentUserCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var expected = ETag.RequireIfMatch(context);

                var result = await expected.Match(
                    version => handler.Handle(
                        request.ToCommand(context.CurrentUser().UserId, version),
                        cancellationToken),
                    error => Task.FromResult(
                        Domain.Shared.Result<Response>.Failure(error))).ConfigureAwait(false);

                return result.Match(
                    updated => ETag.Ok(context, updated, updated.Version),
                    CustomResults.Problem);
            })
            .WithName("updateCurrentUserV1")
            .WithTags(Tags.Users)
            .WithSummary("Rename your account")
            .WithDescription("Requires If-Match with the version you last read.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .RequireAuthorization();
    }
}
