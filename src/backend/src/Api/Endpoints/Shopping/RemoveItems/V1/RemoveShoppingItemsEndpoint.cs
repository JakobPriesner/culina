using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Shopping;
using Response = Contracts.Shopping.Response;

namespace Api.Endpoints.Shopping.RemoveItems.V1;

/// <summary>Takes a line off, or clears what has been bought.</summary>
internal sealed class RemoveShoppingItemsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/households/{{householdId:guid}}/shopping-list/items", async (
                Guid householdId,
                HttpContext context,
                ICommandHandler<RemoveShoppingItemsCommand, Response> handler,
                CancellationToken cancellationToken) =>
            {
                // No id clears everything ticked, which is the one bulk action
                // worth having: after a shop, removing a dozen lines one at a
                // time is the tedium the list exists to avoid.
                var itemId = Guid.TryParse(context.Request.Query["itemId"], out var parsed)
                    ? parsed
                    : (Guid?)null;

                var result = await handler
                    .Handle(
                        new RemoveShoppingItemsCommand(
                            householdId,
                            context.CurrentUser().UserId,
                            itemId),
                        cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("removeShoppingItemsV1")
            .WithTags(Tags.Shopping)
            .WithSummary("Remove a line, or clear what is bought")
            .WithDescription("Pass `itemId` for one line; omit it to clear everything ticked off.")
            .WithRepeatableQueryParameters(["itemId"], [])
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
