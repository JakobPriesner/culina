using Api.Extensions;
using Api.Infrastructure;
using Application.Recipes.Intake;
using Contracts.Recipes.Intake;

namespace Api.Endpoints.RecipeDrafts.WatchIntakes.V1;

/// <summary>Streams the person's imports as they change.</summary>
internal sealed class WatchRecipeIntakesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/recipe-intakes/events", (Guid? attending, HttpContext context, IntakeWatch watch, CancellationToken cancellationToken) =>
                TypedResults.ServerSentEvents(watch.WatchAsync(context.CurrentUser().UserId, IntakeWatch.Heartbeat, attending, cancellationToken)))
            .WithQueryParameters("attending")
            .WithName("watchRecipeIntakesV1")
            .WithTags(Tags.Recipes)
            .WithSummary("Follow the person's imports as they change")
            .WithDescription("Server-sent events. The first one is a snapshot of every unreviewed import, and so is the first one after a reconnect: "
                + "a reader replaces what it has. Each later one carries the imports that changed, a reviewed one meaning it is gone. "
                + "A quiet stream sends one with no imports every fifteen seconds, so a proxy does not close it.\n\n"
                + "Composer drafts (`purpose: draft`) travel on the same stream. `attending` names the draft a page is showing: while a "
                + "stream attends it, its being ready is not announced by a notification. A page closes its stream when it is hidden.")
            .Produces<IntakeEvent>(200, "text/event-stream")
            .ProducesProblem(401)
            .RequireAuthorization();
    }
}
