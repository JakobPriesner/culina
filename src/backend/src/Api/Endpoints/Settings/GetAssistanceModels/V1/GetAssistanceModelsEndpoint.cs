using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Settings.GetAssistanceModels;
using Response = Contracts.Settings.GetAssistanceModels.Response;

namespace Api.Endpoints.Settings.GetAssistanceModels.V1;

/// <summary>Lists what each connected provider currently offers.</summary>
internal sealed class GetAssistanceModelsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/settings/assistance/models", async (
                IQueryHandler<GetAssistanceModelsQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetAssistanceModelsQuery(), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getAssistanceModelsV1")
            .WithTags(Tags.Settings)
            .WithSummary("List what each connected provider offers")
            .WithDescription(
                "Instance administrator only. Asks every connected provider what models it "
                + "has, so choosing one is choosing from a list rather than typing a name "
                + "correctly.\n\n"
                + "A provider that does not answer is a row with `reachable: false` and the "
                + "reason, not a failure — one unreachable provider must not cost the other "
                + "two. It is also the first place a wrong key shows up, which is most of why "
                + "the reason is carried.\n\n"
                + "`canDraw` is the adapter reading the model's name, because none of the "
                + "three providers states it. Only providers that are connected appear at "
                + "all.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(AdminPolicy.Name);
    }
}
