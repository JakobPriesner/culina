using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Settings;
using Application.Settings.UpdateDatabase;
using Request = Contracts.Settings.UpdateDatabase.Request;

namespace Api.Endpoints.Settings.UpdateDatabase.V1;

/// <summary>Points this instance at a database.</summary>
internal sealed class UpdateDatabaseSettingsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/settings/database", async (
                Request request,
                ICommandHandler<UpdateDatabaseSettingsCommand, ServerChange> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new UpdateDatabaseSettingsCommand(
                            request.Host,
                            request.Port,
                            request.Name,
                            request.Username,
                            request.Password,
                            request.RequireSsl,
                            request.MaxPoolSize),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(ServerChangeResults.Of, CustomResults.Problem);
            })
            .WithName("updateDatabaseSettingsV1")
            .WithTags(Tags.Settings)
            .WithSummary("Change the database settings")
            .WithDescription(
                "Instance administrator, or anyone while nobody has an account. Connects first, and "
                + "refuses a database Culina could not run in. `204` when nothing changed; `202` when "
                + "the settings were saved and the server is restarting to use them.")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(AdminPolicy.OrSetupName);
    }
}
