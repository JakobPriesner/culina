using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Users.GetPreferences;
using Response = Contracts.Users.GetPreferences.Response;

namespace Api.Endpoints.Users.GetPreferences.V1;

/// <summary>Reads your preferences.</summary>
internal sealed class GetPreferencesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/users/me/settings", async (
                HttpContext context,
                IQueryHandler<GetPreferencesQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetPreferencesQuery(context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    preferences => ETag.Ok(context, preferences, preferences.Version),
                    CustomResults.Problem);
            })
            .WithName("getUserSettingsV1")
            .WithTags(Tags.Users)
            .WithSummary("Read your preferences")
            .WithDescription("Language, theme, appearance and units. Defaults until first changed.")
            .Produces<Response>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }
}
