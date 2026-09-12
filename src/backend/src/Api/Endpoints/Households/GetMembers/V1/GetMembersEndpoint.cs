using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Households.GetMembers;
using Response = Contracts.Households.GetMembers.Response;

namespace Api.Endpoints.Households.GetMembers.V1;

/// <summary>Lists a household's members.</summary>
internal sealed class GetMembersEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households/{{householdId:guid}}/members", async (
                Guid householdId,
                HttpContext context,
                IQueryHandler<GetMembersQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new GetMembersQuery(householdId, context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getHouseholdMembersV1")
            .WithTags(Tags.Households)
            .WithSummary("List a household's members")
            .WithDescription("Members only. A non-member sees a 404.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
