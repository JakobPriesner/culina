using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Settings.GetMail;
using Response = Contracts.Settings.GetMail.Response;

namespace Api.Endpoints.Settings.GetMail.V1;

/// <summary>Reads the mail account.</summary>
internal sealed class GetMailSettingsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/settings/mail", async (
                IQueryHandler<GetMailSettingsQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new GetMailSettingsQuery(), cancellationToken).ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getMailSettingsV1")
            .WithTags(Tags.Settings)
            .WithSummary("Read the mail account")
            .WithDescription("Instance administrator only. Never returns the password, only whether one is set.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(AdminPolicy.Name);
    }
}
