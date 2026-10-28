using Application.Abstractions;
using Application.Abstractions.Messaging;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Contracts.Products;
using Domain.Products;
using Domain.Shared;

namespace Application.Products;

/// <summary>Looks a barcode up at Open Food Facts, when this server does.</summary>
/// <param name="Code">The barcode as scanned or typed; normalised here.</param>
public sealed record GetCatalogueProductQuery(string Code);

/// <summary>Searches Open Food Facts by name, once, when a person asked for it.</summary>
/// <param name="Query">What was typed.</param>
public sealed record SearchCatalogueQuery(string? Query);

internal sealed class GetCatalogueProductQueryHandler(ProductSettings settings, IProductCatalogue catalogue)
    : IQueryHandler<GetCatalogueProductQuery, CatalogueProductDetail>
{
    public async Task<Result<CatalogueProductDetail>> Handle(
        GetCatalogueProductQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Products.GetFromCatalogue");

        if (!settings.LooksUp)
        {
            return tracked.Record(Result<CatalogueProductDetail>.Failure(ProductErrors.CatalogueOff));
        }

        var result = await Gtin.Create(query.Code).Match(
            async gtin => (await catalogue.FindAsync(gtin, cancellationToken).ConfigureAwait(false))
                .Map(found => found.ToContract()),
            error => Task.FromResult(Result<CatalogueProductDetail>.Failure(error))).ConfigureAwait(false);

        return tracked.Record(result);
    }
}

internal sealed class SearchCatalogueQueryHandler(ProductSettings settings, IProductCatalogue catalogue)
    : IQueryHandler<SearchCatalogueQuery, CatalogueProductsResponse>
{
    private const int ShortestQuery = 2;

    private const int LongestQuery = 100;

    public async Task<Result<CatalogueProductsResponse>> Handle(
        SearchCatalogueQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        using var tracked = UseCaseActivity.Start("Products.SearchCatalogue");

        if (!settings.LooksUp)
        {
            return tracked.Record(Result<CatalogueProductsResponse>.Failure(ProductErrors.CatalogueOff));
        }

        var words = query.Query?.Trim() ?? string.Empty;

        if (words.Length is < ShortestQuery or > LongestQuery)
        {
            return tracked.Record(Result<CatalogueProductsResponse>.Failure(ProductErrors.InvalidCatalogueQuery));
        }

        var found = await catalogue.SearchAsync(words, cancellationToken).ConfigureAwait(false);

        return tracked.Record(found.Map(products =>
            new CatalogueProductsResponse { Items = [.. products.Select(product => product.ToContract())] }));
    }
}

/// <summary>Open Food Facts products to their wire shape.</summary>
internal static class CatalogueMappings
{
    /// <summary>Where Open Food Facts shows a product to people.</summary>
    private const string ProductPage = "https://world.openfoodfacts.org/product/";

    internal static CatalogueProductDetail ToContract(this CatalogueProduct product)
    {
        var values = product.Per100Grams;

        return new CatalogueProductDetail
        {
            Gtin = product.Gtin.Value,
            Name = product.Name,
            Brand = product.Brand,
            Quantity = product.Quantity,
            PackGrams = product.PackGrams,
            ServingGrams = product.ServingGrams,
            EnergyKj = values.EnergyKj,
            EnergyKcal = values.EnergyKcal,
            Fat = values.Fat,
            SaturatedFat = values.SaturatedFat,
            Carbohydrate = values.Carbohydrate,
            Sugars = values.Sugars,
            Protein = values.Protein,
            Salt = values.Salt,
            Doubts = product.Doubts(),
            Url = ProductPage + product.Gtin.Value
        };
    }
}
