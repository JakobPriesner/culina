using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Products;
using Domain.Shared;

namespace Api.Endpoints.Products.RemoveProduct.V1;

/// <summary>Forgets what a household said a barcode is.</summary>
internal sealed class RemoveProductEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapDelete($"{ApiPaths.V1}/households/{{householdId:guid}}/products/{{gtin}}", async (
                Guid householdId,
                string gtin,
                HttpContext context,
                ICommandHandler<RemoveProductCommand> handler,
                CancellationToken cancellationToken) =>
            {
                var expected = ETag.RequireIfMatch(context);

                var result = await expected.Match(
                    version => handler.Handle(
                        new RemoveProductCommand(householdId, context.CurrentUser().UserId, gtin, version),
                        cancellationToken),
                    error => Task.FromResult(Result.Failure(error))).ConfigureAwait(false);

                return result.Match(Results.NoContent, CustomResults.Problem);
            })
            .WithName("removeHouseholdProductV1")
            .WithTags(Tags.Products)
            .WithSummary("Forget what a barcode is")
            .WithDescription(
                "Removes this household's own row; a household it inherits from keeps its own, so an heir that "
                + "removes a row it overrode sees the ancestor's again. Requires `If-Match` with the version "
                + "you last read (428 without, 412 when the row moved on). 204, also when it is already gone.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .RequireAuthorization();
    }
}
