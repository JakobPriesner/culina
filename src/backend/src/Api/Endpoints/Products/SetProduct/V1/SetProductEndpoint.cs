using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Products;
using Contracts.Products;
using Domain.Shared;
using Request = Contracts.Products.SetProductRequest;

namespace Api.Endpoints.Products.SetProduct.V1;

/// <summary>Says what a barcode is in a household.</summary>
internal sealed class SetProductEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPut($"{ApiPaths.V1}/households/{{householdId:guid}}/products/{{gtin}}", async (
                Guid householdId,
                string gtin,
                Request request,
                HttpContext context,
                ICommandHandler<SetProductCommand, ProductDetail> handler,
                CancellationToken cancellationToken) =>
            {
                var userId = context.CurrentUser().UserId;

                Task<Result<ProductDetail>> Send(long? version) => handler.Handle(
                    new SetProductCommand(householdId, userId, gtin, request, version),
                    cancellationToken);

                // Without If-Match the barcode is being named; with it, a row the caller read is being changed.
                var named = context.Request.Headers.IfMatch.Count == 0;

                var result = named
                    ? await Send(null).ConfigureAwait(false)
                    : await ETag.RequireIfMatch(context).Match(
                        version => Send(version),
                        error => Task.FromResult(Result<ProductDetail>.Failure(error))).ConfigureAwait(false);

                return result.Match(
                    product =>
                    {
                        context.Response.Headers.ETag = ETag.Of(product.Version, householdId);
                        context.Response.Headers.CacheControl = "private, no-cache";

                        return named
                            ? Results.Created($"{ApiPaths.V1}/households/{householdId}/products/{product.Gtin}", product)
                            : Results.Ok(product);
                    },
                    CustomResults.Problem);
            })
            .WithName("setHouseholdProductV1")
            .WithTags(Tags.Products)
            .WithSummary("Say what a barcode is")
            .WithDescription(
                "Names a barcode for this household, or changes what it said. Without `If-Match` the barcode "
                + "is being named: 201 with the new `ETag`, or 428 `products.already_named` when this household "
                + "already has it (two phones naming the same code at once: the later one reads the row and "
                + "decides). With `If-Match` from a read of the household's own row it is replaced: 200 with the "
                + "new `ETag`, 412 when the row moved on, 404 `products.unknown` when it is gone. A household "
                + "that only inherits the barcode names its own row without `If-Match`; the ancestor's is "
                + "never touched. `{gtin}` is read as for GET and stored normalised. `name` is required; "
                + "`ingredient`, `quantity` (with `unit`), `cookMinutes` and `note` are optional, and "
                + "`cookMinutes` is only ever what the household says: a barcode does not supply one. Any member may.")
            .Produces<ProductDetail>(StatusCodes.Status201Created)
            .Produces<ProductDetail>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status412PreconditionFailed)
            .ProducesProblem(StatusCodes.Status428PreconditionRequired)
            .RequireAuthorization();
    }
}
