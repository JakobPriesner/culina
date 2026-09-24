using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Settings.GetDatabase;
using Response = Contracts.Settings.GetDatabase.Response;

namespace Api.Endpoints.Settings.GetDatabase.V1;

/// <summary>Reads how this instance reaches PostgreSQL.</summary>
internal sealed class GetDatabaseSettingsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/settings/database", async (
                IQueryHandler<GetDatabaseSettingsQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetDatabaseSettingsQuery(), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getDatabaseSettingsV1")
            .WithTags(Tags.Settings)
            .WithSummary("Get the database settings")
            .WithDescription(
                "Instance administrator, or anyone while nobody has an account. Never returns the "
                + "password — only whether one is set. `pinned` names the settings the deployment's "
                + "environment fixes.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(AdminPolicy.OrSetupName);
    }
}
