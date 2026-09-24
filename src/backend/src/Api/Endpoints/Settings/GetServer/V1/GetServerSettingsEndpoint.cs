using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Settings.GetServer;
using Response = Contracts.Settings.GetServer.Response;

namespace Api.Endpoints.Settings.GetServer.V1;

/// <summary>Reads the server settings the running process uses.</summary>
internal sealed class GetServerSettingsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/settings/server", async (
                HttpContext context,
                IQueryHandler<GetServerSettingsQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(context.ToQuery(), cancellationToken).ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getServerSettingsV1")
            .WithTags(Tags.Settings)
            .WithSummary("Get the server settings")
            .WithDescription(
                "Instance administrator, or anyone while nobody has an account. Cookies, trusted "
                + "proxies, rate limits and telemetry as the running process uses them; `pinned` "
                + "names the settings the deployment's environment fixes, and `connection` how this "
                + "request reached the server.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(AdminPolicy.OrSetupName);
    }
}
