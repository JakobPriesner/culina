using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions;

namespace Api.Endpoints.Push.RemoveSubscription.V1;

/// <summary>Forgets a browser's Web Push subscription.</summary>
internal sealed class RemovePushEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/push/subscription", async (
                string endpoint,
                HttpContext context,
                IIntakeNotifications push,
                CancellationToken cancellationToken) =>
            {
                await push.RemoveAsync(context.CurrentUser().UserId, endpoint, cancellationToken).ConfigureAwait(false);

                return Results.NoContent();
            })
            .WithQueryParameters("endpoint")
            .WithName("removePushV1")
            .WithTags(Tags.Recipes)
            .RequireAuthorization();
    }
}
