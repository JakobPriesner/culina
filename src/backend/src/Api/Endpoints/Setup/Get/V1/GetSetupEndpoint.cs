using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Setup.Get;
using Response = Contracts.Setup.Get.Response;

namespace Api.Endpoints.Setup.Get.V1;

/// <summary>Reads how far this instance has got in being set up.</summary>
internal sealed class GetSetupEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/setup", async (
                IQueryHandler<GetSetupQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new GetSetupQuery(), cancellationToken).ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getSetupV1")
            .WithTags(Tags.Setup)
            .WithSummary("Get how far setup has got")
            .WithDescription(
                "Anonymous. `database` until a database is configured, `account` until somebody has "
                + "an account, then `complete`. `startedAt` changes whenever the server restarts to "
                + "apply a setting.")
            .Produces<Response>()
            .AllowAnonymous();
    }
}
