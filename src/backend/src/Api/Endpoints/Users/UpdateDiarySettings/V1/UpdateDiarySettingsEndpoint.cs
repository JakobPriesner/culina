using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Users.UpdateDiarySettings;
using Request = Contracts.Users.UpdateDiarySettings.Request;
using Response = Contracts.Users.UpdateDiarySettings.Response;

namespace Api.Endpoints.Users.UpdateDiarySettings.V1;

/// <summary>Switches your diary on or off and sets your targets.</summary>
internal sealed class UpdateDiarySettingsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/users/me/settings/diary", async (
                Request request,
                HttpContext context,
                ICommandHandler<UpdateDiarySettingsCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(request.ToCommand(context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    settings => ETag.Ok(context, settings, settings.Version, context.CurrentUser().UserId),
                    CustomResults.Problem);
            })
            .WithName("updateDiarySettingsV1")
            .WithTags(Tags.Users)
            .WithSummary("Change your diary settings")
            .WithDescription(
                "Replaces every value. Switching on needs consentVersion (the currentConsentVersion you were shown) "
                + "and records your agreement. Switching off keeps your entries hidden, or deletes them with "
                + "eraseEntries. No If-Match: one person's own settings, with no second writer.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }
}
