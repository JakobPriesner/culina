using Api.Extensions;
using Api.Infrastructure;
using Application.Abstractions.Messaging;
using Application.Products;
using Contracts.Products;

namespace Api.Endpoints.Products.SearchCatalogue.V1;

/// <summary>Searches Open Food Facts by name, when a person asked for it.</summary>
internal sealed class SearchCatalogueEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet($"{ApiPaths.V1}/products", async (
                HttpContext context,
                IQueryHandler<SearchCatalogueQuery, CatalogueProductsResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var result = await handler
                    .Handle(new SearchCatalogueQuery(context.Request.Query["q"]), cancellationToken)
                    .ConfigureAwait(false);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithName("searchCatalogueV1")
            .WithTags(Tags.Products)
            .WithSummary("Search Open Food Facts by name")
            .WithDescription(
                "Up to 20 products sold in Germany whose name fits `q` (2 to 100 characters), best fit first. "
                + "One request to Open Food Facts per call, and it allows only a few a minute: call it when a person "
                + "asks, never while they type. A result lacks its pack size; `/products/{gtin}` gives the whole product "
                + "when one is chosen. Same errors as `/products/{gtin}`, and 400 `products.invalid_query`.")
            .WithQueryParameters("q")
            .Produces<CatalogueProductsResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization();
    }
}
