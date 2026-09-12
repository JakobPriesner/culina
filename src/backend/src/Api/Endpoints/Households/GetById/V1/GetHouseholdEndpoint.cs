using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Households.GetById;
using Response = Contracts.Households.GetById.Response;

namespace Api.Endpoints.Households.GetById.V1;

/// <summary>Reads one household.</summary>
internal sealed class GetHouseholdEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households/{{householdId:guid}}", async (
                Guid householdId,
                HttpContext context,
                IQueryHandler<GetHouseholdQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new GetHouseholdQuery(householdId, context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    household => ETag.Ok(context, household, household.Version),
                    CustomResults.Problem);
            })
            .WithName("getHouseholdByIdV1")
            .WithTags(Tags.Households)
            .WithSummary("Read a household")
            .WithDescription("A household and its members. A non-member sees a 404, never a 403.")
            .Produces<Response>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
