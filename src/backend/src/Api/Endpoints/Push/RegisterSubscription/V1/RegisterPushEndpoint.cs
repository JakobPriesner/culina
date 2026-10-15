using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions;
using Contracts.Recipes.Intake;

namespace Api.Endpoints.Push.RegisterSubscription.V1;

/// <summary>Registers a browser's Web Push subscription.</summary>
internal sealed class RegisterPushEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/push/subscription", async (
                PushRegistration subscription,
                HttpContext context,
                IIntakeNotifications push,
                CancellationToken cancellationToken) =>
            {
                var result = await push.RegisterAsync(context.CurrentUser().UserId, subscription, cancellationToken).ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("registerPushV1")
            .WithTags(Tags.Recipes)
            .RequireAuthorization();
    }
}
