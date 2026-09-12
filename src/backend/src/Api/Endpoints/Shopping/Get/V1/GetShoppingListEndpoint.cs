using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Shopping;
using Response = Contracts.Shopping.Response;

namespace Api.Endpoints.Shopping.Get.V1;

/// <summary>Reads a household's shopping list.</summary>
internal sealed class GetShoppingListEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households/{{householdId:guid}}/shopping-list", async (
                Guid householdId,
                HttpContext context,
                IQueryHandler<GetShoppingListQuery, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new GetShoppingListQuery(householdId, context.CurrentUser().UserId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(
                    list => ETag.Ok(context, list, list.Version, list.ListId),
                    CustomResults.Problem);
            })
            .WithName("getShoppingListV1")
            .WithTags(Tags.Shopping)
            .WithSummary("Read the shopping list")
            .WithDescription(
                "One list per household, created the first time anyone looks. Amounts are "
                + "**unrounded**: summing rounded amounts compounds error, and how a number is "
                + "shown is the client's business.")
            .Produces<Response>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
