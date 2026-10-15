using Domain.Products;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>A barcode a household named, and which household did.</summary>
/// <param name="Product">What the household said.</param>
/// <param name="HouseholdId">The household that said it.</param>
/// <param name="Version">The row's version, for <c>If-Match</c>.</param>
public sealed record StoredProduct(HouseholdProduct Product, Guid HouseholdId, long Version);

/// <summary>Keeps what households say their barcodes are.</summary>
public interface IProductRepository
{
    /// <summary>
    /// The products named in a library, one per barcode: the nearest household's wins.
    /// </summary>
    /// <param name="library">The household first, then what it inherits, nearest first.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<IReadOnlyList<StoredProduct>> ForLibraryAsync(IReadOnlyList<Guid> library, CancellationToken cancellationToken);

    /// <summary>The nearest household's product for a barcode, or null when none named it.</summary>
    /// <param name="library">The household first, then what it inherits, nearest first.</param>
    /// <param name="gtin">The barcode.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<StoredProduct?> FindAsync(IReadOnlyList<Guid> library, Gtin gtin, CancellationToken cancellationToken);

    /// <summary>Keeps a product the household has not named yet; <see cref="ProductErrors.AlreadyNamed"/> when it has. Returns version 1.</summary>
    Task<Result<long>> AddAsync(Guid householdId, Guid userId, HouseholdProduct product, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the household's own product when its version is still the one the caller read:
    /// <see cref="ProductErrors.Unknown"/> when the household has none, a version mismatch when it moved on.
    /// Returns the new version.
    /// </summary>
    Task<Result<long>> UpdateAsync(
        Guid householdId,
        HouseholdProduct product,
        long expectedVersion,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes the household's own product when its version is still the one the caller read; having none is
    /// fine, a version that moved on is a mismatch.
    /// </summary>
    Task<Result> RemoveAsync(Guid householdId, Gtin gtin, long expectedVersion, CancellationToken cancellationToken);
}
