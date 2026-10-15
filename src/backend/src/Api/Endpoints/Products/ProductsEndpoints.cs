using Api.Endpoints.Products.GetProduct.V1;
using Api.Endpoints.Products.ListProducts.V1;
using Api.Endpoints.Products.RemoveProduct.V1;
using Api.Endpoints.Products.SetProduct.V1;

namespace Api.Endpoints.Products;

/// <summary>The product memory endpoints, registered explicitly.</summary>
internal static class ProductsEndpoints
{
    internal static IServiceCollection AddProductsEndpoints(this IServiceCollection services) =>
        services
            .AddSingleton<IEndpoint, ListProductsEndpoint>()
            .AddSingleton<IEndpoint, GetProductEndpoint>()
            .AddSingleton<IEndpoint, SetProductEndpoint>()
            .AddSingleton<IEndpoint, RemoveProductEndpoint>();
}
