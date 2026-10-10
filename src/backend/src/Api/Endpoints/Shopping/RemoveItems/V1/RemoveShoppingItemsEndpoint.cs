using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Shopping;
using Domain.Shared;
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
                // No id clears everything ticked: the one bulk action worth having after a shop. A bad id is
                // refused, not read as "no id", which would clear every ticked line.
                var command = context.Request.Query.ReadGuid("itemId").Map(itemId =>
                    new RemoveShoppingItemsCommand(householdId, context.CurrentUser().UserId, itemId.Value));

                return await command.Match(
                    async removing =>
                    {
                        var result = await handler.Handle(removing, cancellationToken).ConfigureAwait(false);

                        return result.Match(Results.Ok, CustomResults.Problem);
                    },
                    error => Task.FromResult(CustomResults.Problem(error))).ConfigureAwait(false);
            })
            .WithName("removeShoppingItemsV1")
            .WithTags(Tags.Shopping)
            .WithSummary("Remove a line, or clear what is bought")
            .WithDescription("Pass `itemId` for one line; omit it to clear everything ticked off.")
            .WithRepeatableQueryParameters(["itemId"], [])
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
