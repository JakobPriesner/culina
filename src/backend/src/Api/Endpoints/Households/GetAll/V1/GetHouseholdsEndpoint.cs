using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Households.GetAll;
using Response = Contracts.Households.GetAll.Response;

namespace Api.Endpoints.Households.GetAll.V1;

/// <summary>Lists the caller's households.</summary>
internal sealed class GetHouseholdsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households", async (
                bool? deleted,
                HttpContext context,
                IQueryHandler<GetHouseholdsQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetHouseholdsQuery(context.CurrentUser().UserId, deleted ?? false), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getHouseholdsV1")
            .WithTags(Tags.Households)
            .WithSummary("List your households")
            .WithDescription(
                "Every household the caller belongs to, by name. With `deleted=true`, the households "
                + "in the bin that the caller owns instead, each with `deletedAt` and `purgeAfter`: "
                + "the ones `POST /households/{householdId}/restorations` can bring back.")
            .WithQueryParameters("deleted")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }
}
