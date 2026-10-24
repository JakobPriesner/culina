using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Settings.SendMailTest;

namespace Api.Endpoints.Settings.SendMailTest.V1;

/// <summary>Sends a test mail to the administrator.</summary>
internal sealed class SendMailTestEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/settings/mail/tests", async (
                HttpContext context,
                ICommandHandler<SendMailTestCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new SendMailTestCommand(context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("sendMailTestV1")
            .WithTags(Tags.Settings)
            .WithSummary("Send a test mail")
            .WithDescription(
                "Instance administrator only. Sends a mail to the administrator's own address through the saved "
                + "account, straight away. mail.send_failed carries what the server answered.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(AdminPolicy.Name);
    }
}
