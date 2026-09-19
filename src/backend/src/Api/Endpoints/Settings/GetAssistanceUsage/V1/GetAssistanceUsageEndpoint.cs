using Api.Authentication;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Settings.GetAssistanceUsage;
using Response = Contracts.Settings.GetAssistanceUsage.Response;

namespace Api.Endpoints.Settings.GetAssistanceUsage.V1;

/// <summary>Reads what the assistant has cost this month.</summary>
internal sealed class GetAssistanceUsageEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/settings/assistance/usage", async (
                IQueryHandler<GetAssistanceUsageQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetAssistanceUsageQuery(), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getAssistanceUsageV1")
            .WithTags(Tags.Settings)
            .WithSummary("Read what the assistant has cost this month")
            .WithDescription(
                "Instance administrator only.\n\nThe calendar month in UTC, because that is "
                + "how a provider bills and a figure measured over a different period could "
                + "not be reconciled with the invoice.\n\n"
                + "`unpriced` counts calls made with a model this app has no price for. Their "
                + "tokens are in the totals and their cost is not, because a number nobody can "
                + "check against a bill is worse than an empty cell.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(AdminPolicy.Name);
    }
}
