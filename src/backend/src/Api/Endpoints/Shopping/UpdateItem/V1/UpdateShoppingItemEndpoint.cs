using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Shopping;
using Request = Contracts.Shopping.UpdateItemRequest;
using Response = Contracts.Shopping.Response;

namespace Api.Endpoints.Shopping.UpdateItem.V1;

/// <summary>Ticks a line off, or moves it to where it actually lives.</summary>
internal sealed class UpdateShoppingItemEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPatch(
                $"{ApiPaths.V1}/households/{{householdId:guid}}/shopping-list/items/{{itemId:guid}}",
                async (
                    Guid householdId,
                    Guid itemId,
                    Request request,
                    HttpContext context,
                    ICommandHandler<UpdateShoppingItemCommand, Response> handler,
                    CancellationToken cancellationToken) =>
                {
                    var result = await handler
                        .Handle(
                            new UpdateShoppingItemCommand(
                                householdId,
                                itemId,
                                context.CurrentUser().UserId,
                                request.IsChecked,
                                request.Section),
                            cancellationToken)
                        .ConfigureAwait(false);

                    return result.Match(Results.Ok, CustomResults.Problem);
                })
            .WithName("updateShoppingItemV1")
            .WithTags(Tags.Shopping)
            .WithSummary("Tick it off, or move it")
            .WithDescription(
                "Moving an item teaches the household where that thing lives, so the correction "
                + "never has to be made twice. That is what replaces a configuration screen.")
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
