using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Users.UpdatePreferences;
using Request = Contracts.Users.UpdatePreferences.Request;
using Response = Contracts.Users.UpdatePreferences.Response;

namespace Api.Endpoints.Users.UpdatePreferences.V1;

/// <summary>Replaces your preferences.</summary>
internal sealed class UpdatePreferencesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/users/me/settings", async (
                Request request,
                HttpContext context,
                ICommandHandler<UpdatePreferencesCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(request.ToCommand(context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    preferences => ETag.Ok(context, preferences, preferences.Version),
                    CustomResults.Problem);
            })
            .WithName("updateUserSettingsV1")
            .WithTags(Tags.Users)
            .WithSummary("Change your preferences")
            .WithDescription(
                "Replaces all four values. No If-Match: these are one person's own preferences, "
                + "edited on one screen, so there is no second writer to conflict with.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }
}
