using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Households.GetHeirs;
using Response = Contracts.Households.GetHeirs.Response;

namespace Api.Endpoints.Households.GetHeirs.V1;

/// <summary>Lists the households that see a household's recipes.</summary>
internal sealed class GetHeirsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households/{{householdId:guid}}/heirs", async (
                Guid householdId,
                HttpContext context,
                IQueryHandler<GetHeirsQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetHeirsQuery(householdId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getHouseholdHeirsV1")
            .WithTags(Tags.Households)
            .WithSummary("List the households that inherit this one's recipes")
            .WithDescription(
                "Members only. Every household that sees this one's recipes: those inheriting from "
                + "it, then those inheriting from them. Not paged: a handful at most.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
