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
                HttpContext context,
                IQueryHandler<GetHouseholdsQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetHouseholdsQuery(context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getHouseholdsV1")
            .WithTags(Tags.Households)
            .WithSummary("List your households")
            .WithDescription("Every household the caller belongs to, by name.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .RequireAuthorization();
    }
}
