using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Shopping;
using Request = Contracts.Shopping.AddItemRequest;
using Response = Contracts.Shopping.Response;

namespace Api.Endpoints.Shopping.AddItem.V1;

/// <summary>Puts something on the list by hand.</summary>
internal sealed class AddShoppingItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost($"{ApiPaths.V1}/households/{{householdId:guid}}/shopping-list/items", async (
                Guid householdId,
                Request request,
                HttpContext context,
                ICommandHandler<AddShoppingItemCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(
                        new AddShoppingItemCommand(
                            householdId,
                            context.CurrentUser().UserId,
                            request.Name,
                            request.Quantity,
                            request.Unit),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("addShoppingItemV1")
            .WithTags(Tags.Shopping)
            .WithSummary("Add something to the list")
            .WithDescription(
                "A hand-typed line is never merged into an existing one: typing \"butter\" when "
                + "butter is already there usually means you want more of it noted, and merging "
                + "silently would hide that you added anything.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
