using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Products;
using Contracts.Products;

namespace Api.Endpoints.Products.ListProducts.V1;

/// <summary>Lists what a household says its barcodes are.</summary>
internal sealed class ListProductsEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/households/{{householdId:guid}}/products", async (
                Guid householdId,
                HttpContext context,
                IQueryHandler<ListProductsQuery, ProductsResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new ListProductsQuery(householdId, context.CurrentUser().UserId), cancellationToken)
                    .ConfigureAwait(false);

                // No version spans the rows, so the tag is the household, how many there are and a fingerprint of
                // every row's owner, barcode and version: any change to what the household sees changes it.
                return result.Match(
                    products => ETag.Ok(
                        context,
                        products,
                        products.Items.Count,
                        householdId,
                        ETag.Fingerprint(products.Items.Select(one => $"{one.HouseholdId:N}/{one.Gtin}/{one.Version}"))),
                    CustomResults.Problem);
            })
            .WithName("listHouseholdProductsV1")
            .WithTags(Tags.Products)
            .WithSummary("List what a household says its barcodes are")
            .WithDescription(
                "Every barcode this household, and the households it inherits from, have named, one entry "
                + "per barcode, the nearest household's when several named it (`householdId` says whose). "
                + "Not paged: it seeds the offline cache, and a household names hundreds of barcodes at most. "
                + "Has an `ETag` and answers `304` to a matching `If-None-Match`. This is a mapping from "
                + "packaging to an ingredient, never what the household has at home.")
            .Produces<ProductsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}
