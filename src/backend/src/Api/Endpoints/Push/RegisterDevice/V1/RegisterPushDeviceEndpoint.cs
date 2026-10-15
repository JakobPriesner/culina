using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions;
using Contracts.Recipes.Intake;

namespace Api.Endpoints.Push.RegisterDevice.V1;

/// <summary>Registers an installed app's device for import notifications.</summary>
internal sealed class RegisterPushDeviceEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/push/devices/{{deviceId:guid}}", async (
                Guid deviceId,
                PushDeviceRegistration device,
                HttpContext context,
                IIntakeNotifications push,
                CancellationToken cancellationToken) =>
            {
                var result = await push
                    .RegisterDeviceAsync(context.CurrentUser().UserId, deviceId, device.RelayToken, device.Language, cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("registerPushDeviceV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Register an installed app's device for import notifications")
            .WithDescription(
                "The app picks the device id and sends the relay token it got from the push relay; the same device signing "
                + "in again, as anyone, replaces its row. Ten per person (409 Push.TooManyDevices). 409 push.relay_off "
                + "when this server names no relay.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization();
    }
}
