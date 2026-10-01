using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.RecoveryCodes.Issue;
using Request = Contracts.RecoveryCodes.Issue.Request;
using Response = Contracts.RecoveryCodes.Issue.Response;

namespace Api.Endpoints.RecoveryCodes.Issue.V1;

/// <summary>Issues a recovery code for somebody who is locked out.</summary>
internal sealed class IssueRecoveryCodeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/recovery-codes", async (
                Request request,
                HttpContext context,
                ICommandHandler<IssueRecoveryCodeCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new IssueRecoveryCodeCommand(context.CurrentUser().UserId, request.Email),
                        cancellationToken)
                    .ConfigureAwait(false);

                // No Location: the code is shown once and has nothing to read
                // back, by design.
                return result.Match(
                    issued => Results.Json(issued, statusCode: StatusCodes.Status201Created),
                    CustomResults.Problem);
            })
            .WithName("issueRecoveryCodeV1")
            .WithTags(Tags.RecoveryCodes)
            .WithSummary("Help somebody back into their account")
            .WithDescription(
                "The administrator's only. Returns a one-time code, good for 24 hours, that sets a "
                + "new password for the account with this address. Pass it on yourself; Culina sends nothing.")
            .Produces<Response>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization(AdminPolicy.Name);
    }
}
