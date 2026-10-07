using System.Globalization;
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
                // No id clears everything ticked: the one bulk action worth having after a shop.
                var raw = context.Request.Query["itemId"];
                Guid? itemId = null;

                // A bad id is refused, not read as "no id", which would clear every ticked line.
                if (raw.Count > 0)
                {
                    if (!Guid.TryParse(raw, CultureInfo.InvariantCulture, out var parsed))
                    {
                        return CustomResults.Problem(
                            new FieldError("itemId", "request.unknown_parameter", "That is not an item id."));
                    }

                    itemId = parsed;
                }

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
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
