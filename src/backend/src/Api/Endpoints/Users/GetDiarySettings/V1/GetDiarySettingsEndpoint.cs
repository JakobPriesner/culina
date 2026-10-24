using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Users.GetDiarySettings;
using Response = Contracts.Users.GetDiarySettings.Response;

namespace Api.Endpoints.Users.GetDiarySettings.V1;

/// <summary>Reads whether you keep a food diary, and your targets.</summary>
internal sealed class GetDiarySettingsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/users/me/settings/diary", async (
                HttpContext context,
                IQueryHandler<GetDiarySettingsQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetDiarySettingsQuery(context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    settings => ETag.Ok(context, settings, settings.Version, context.CurrentUser().UserId),
                    CustomResults.Problem);
            })
            .WithName("getDiarySettingsV1")
            .WithTags(Tags.Users)
            .WithSummary("Read your diary settings")
            .WithDescription(
                "Off until you switch it on. currentConsentVersion is the wording switching on agrees to; "
                + "consentedAt and consentVersion record your last agreement.")
            .Produces<Response>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }
}
