using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Settings.GetAssistance;
using Response = Contracts.Settings.GetAssistance.Response;

namespace Api.Endpoints.Settings.GetAssistance.V1;

/// <summary>Reads how the assistant is set up.</summary>
internal sealed class GetAssistanceSettingsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/settings/assistance", async (
                IQueryHandler<GetAssistanceSettingsQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetAssistanceSettingsQuery(), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getAssistanceSettingsV1")
            .WithTags(Tags.Settings)
            .WithSummary("Read how the assistant is set up")
            .WithDescription(
                "Instance administrator only.\n\nThe API key is never returned. "
                + "`apiKeyConfigured` says whether one has been entered, which is the only "
                + "thing about it a screen needs to know.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(AdminPolicy.Name);
    }
}
