using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions;

namespace Api.Endpoints.Push.RemoveDevice.V1;

/// <summary>Forgets an installed app's device, on sign-out.</summary>
internal sealed class RemovePushDeviceEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/push/devices/{{deviceId:guid}}", async (
                Guid deviceId,
                HttpContext context,
                IIntakeNotifications push,
                CancellationToken cancellationToken) =>
            {
                await push.RemoveDeviceAsync(context.CurrentUser().UserId, deviceId, cancellationToken).ConfigureAwait(false);

                return Results.NoContent();
            })
            .WithName("removePushDeviceV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Forget an installed app's device")
            .WithDescription("On sign-out. 204, also when it was never registered.")
            .RequireAuthorization();
    }
}
