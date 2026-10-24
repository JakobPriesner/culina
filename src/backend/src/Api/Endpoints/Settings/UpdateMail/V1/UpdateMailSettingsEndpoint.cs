using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Settings.UpdateMail;
using Request = Contracts.Settings.UpdateMail.Request;
using Response = Contracts.Settings.GetMail.Response;

namespace Api.Endpoints.Settings.UpdateMail.V1;

/// <summary>Changes the mail account.</summary>
internal sealed class UpdateMailSettingsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/settings/mail", async (
                Request request,
                ICommandHandler<UpdateMailSettingsCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new UpdateMailSettingsCommand(
                            request.Enabled,
                            request.Host,
                            request.Port,
                            request.Security,
                            request.Username,
                            request.Password,
                            request.FromAddress,
                            request.FromName),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("updateMailSettingsV1")
            .WithTags(Tags.Settings)
            .WithSummary("Change the mail account")
            .WithDescription(
                "Instance administrator only. Takes effect at once, with no restart. While mail is on, new accounts "
                + "must confirm their address before they can sign in, and sign-in links, password-forgotten links "
                + "and account notices are mailed.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(AdminPolicy.Name);
    }
}
