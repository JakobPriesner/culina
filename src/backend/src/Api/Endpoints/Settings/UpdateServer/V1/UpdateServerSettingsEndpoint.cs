using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Settings;
using Application.Settings.UpdateServer;
using Request = Contracts.Settings.UpdateServer.Request;

namespace Api.Endpoints.Settings.UpdateServer.V1;

/// <summary>Changes the server settings.</summary>
internal sealed class UpdateServerSettingsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/settings/server", async (
                Request request,
                ICommandHandler<UpdateServerSettingsCommand, ServerChange> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(request.ToCommand(), cancellationToken).ConfigureAwait(false);

                return result.Match(ServerChangeResults.Of, CustomResults.Problem);
            })
            .WithName("updateServerSettingsV1")
            .WithTags(Tags.Settings)
            .WithSummary("Change the server settings")
            .WithDescription(
                "Instance administrator, or anyone while nobody has an account. Validated exactly as "
                + "the next startup will validate it. Settings the environment pins are not saved. "
                + "`204` when nothing changed; `202` when the settings were saved and the server is "
                + "restarting to use them.")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(AdminPolicy.OrSetupName);
    }
}
