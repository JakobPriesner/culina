using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions;
using Contracts.Recipes.Intake;

namespace Api.Endpoints.Push.GetKey.V1;

/// <summary>The server's Web Push public key.</summary>
internal sealed class GetPushKeyEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/push/key", async (IIntakeNotifications push, CancellationToken cancellationToken) =>
                Results.Ok(new PushKey(await push.PublicKeyAsync(cancellationToken).ConfigureAwait(false))))
            .WithName("getPushKeyV1")
            .WithTags(Tags.Recipes)
            .Produces<PushKey>()
            .RequireAuthorization();
    }
}
