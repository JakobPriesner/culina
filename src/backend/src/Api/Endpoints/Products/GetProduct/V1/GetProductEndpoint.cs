using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Products;
using Contracts.Products;

namespace Api.Endpoints.Products.GetProduct.V1;

/// <summary>Reads what a household says one barcode is.</summary>
internal sealed class GetProductEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households/{{householdId:guid}}/products/{{gtin}}", async (
                Guid householdId,
                string gtin,
                HttpContext context,
                IQueryHandler<GetProductQuery, ProductDetail> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new GetProductQuery(householdId, context.CurrentUser().UserId, gtin), cancellationToken)
                    .ConfigureAwait(false);

                // The row may be an ancestor's, so the tag names the household whose row it is.
                return result.Match(
                    product => ETag.Ok(context, product, product.Version, product.HouseholdId),
                    CustomResults.Problem);
            })
            .WithName("getHouseholdProductV1")
            .WithTags(Tags.Products)
            .WithSummary("Read what a household says a barcode is")
            .WithDescription(
                "The household's own memory of the barcode, else the nearest household it inherits from "
                + "(`householdId` in the body says whose). `{gtin}` is the digits of an EAN-8, UPC-A, "
                + "EAN-13 or GTIN-14, or an eight-digit UPC-E, with a correct check digit; the response "
                + "carries the normalised form (a UPC-A or UPC-E is padded to 13 digits). 404 "
                + "`products.unknown` when nobody named it: the caller then asks for a name and PUTs it. "
                + "Has an `ETag`; send it as `If-Match` to change or remove the household's own row.")
            .Produces<ProductDetail>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
