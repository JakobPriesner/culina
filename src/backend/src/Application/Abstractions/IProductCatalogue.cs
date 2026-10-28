using Domain.Products;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>
/// Open Food Facts, asked by the server only (the browser keeps <c>connect-src 'self'</c>), within its request limits,
/// and remembered for a while so a scanned code is not asked again on every scan.
/// </summary>
public interface IProductCatalogue
{
    /// <summary>
    /// The product with this barcode, from what was asked recently or else from Open Food Facts;
    /// <see cref="ProductErrors.NotInCatalogue"/> when it knows none, <see cref="ProductErrors.CatalogueBusy"/> when the
    /// server is at its request limit, <see cref="ProductErrors.CatalogueUnavailable"/> when it did not answer.
    /// </summary>
    /// <param name="gtin">The barcode.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    Task<Result<CatalogueProduct>> FindAsync(Gtin gtin, CancellationToken cancellationToken);

    /// <summary>What the server last heard for this barcode, without asking: what a person was shown.</summary>
    /// <param name="gtin">The barcode.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<CatalogueProduct?> RememberedAsync(Gtin gtin, CancellationToken cancellationToken);

    /// <summary>Products sold in Germany whose name fits the words, best fit first; one request to Open Food Facts.</summary>
    /// <param name="query">What was typed.</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    Task<Result<IReadOnlyList<CatalogueProduct>>> SearchAsync(string query, CancellationToken cancellationToken);
}
