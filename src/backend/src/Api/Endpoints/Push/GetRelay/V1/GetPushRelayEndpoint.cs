using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Settings;
using Contracts.Recipes.Intake;

namespace Api.Endpoints.Push.GetRelay.V1;

/// <summary>Whether the installed apps can be notified.</summary>
internal sealed class GetPushRelayEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/push/relay", (PushSettings settings) => Results.Ok(new PushRelay(settings.RelayOn)))
            .WithName("getPushRelayV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Whether the installed apps can be notified")
            .WithDescription("True when an administrator named a push relay (Push:RelayUrl). An app asks for notification permission only when it is.")
            .Produces<PushRelay>()
            .RequireAuthorization();
    }
}
