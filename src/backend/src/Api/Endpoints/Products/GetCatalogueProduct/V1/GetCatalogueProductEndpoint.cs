using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Products;
using Contracts.Products;

namespace Api.Endpoints.Products.GetCatalogueProduct.V1;

/// <summary>Looks one barcode up at Open Food Facts.</summary>
internal sealed class GetCatalogueProductEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/products/{{gtin}}", async (
                string gtin,
                IQueryHandler<GetCatalogueProductQuery, CatalogueProductDetail> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler.Handle(new GetCatalogueProductQuery(gtin), cancellationToken).ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("getCatalogueProductV1")
            .WithTags(Tags.Products)
            .WithSummary("Look a barcode up at Open Food Facts")
            .WithDescription(
                "What Open Food Facts says the product is, per 100 g, with `doubts` to check before taking it over. "
                + "Reference data, not household data: ask the household's memory "
                + "(`/households/{householdId}/products/{gtin}`) first, since what a household said wins. The server "
                + "asks Open Food Facts at most once a month per code (a week for a code it does not know) and keeps "
                + "under its request limit. 404 `products.catalogue_off` when the administrator switched the lookup off "
                + "(`openFoodFacts` on `/users/me` says so), 404 `products.not_in_catalogue` when Open Food "
                + "Facts does not know the code, 429 `products.catalogue_busy`, 503 `products.catalogue_unavailable`.")
            .Produces<CatalogueProductDetail>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization();
    }
}
