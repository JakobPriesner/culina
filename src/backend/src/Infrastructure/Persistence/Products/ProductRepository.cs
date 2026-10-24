using Application.Abstractions;
using Domain.Products;
using Domain.Shared;

namespace Infrastructure.Persistence.Products;

internal sealed class ProductRepository(DbExecutor executor, TimeProvider time) : IProductRepository
{
    // Per barcode, the nearest household that named it decides.
    private const string Columns =
        "household_id, gtin, name, ingredient, quantity, unit, cook_minutes, note, version";

    public async Task<IReadOnlyList<StoredProduct>> ForLibraryAsync(
        IReadOnlyList<Guid> library,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(library);

        var rows = await executor.QueryAsync<ProductRow>(
            $"""
            select distinct on (gtin) {Columns}
            from household_products
            where household_id = any(@library)
            order by gtin, array_position(@library, household_id);
            """,
            new { library = library.ToArray() },
            cancellationToken).ConfigureAwait(false);

        return [.. rows.Select(row => row.ToStored())];
    }

    public async Task<StoredProduct?> FindAsync(
        IReadOnlyList<Guid> library,
        Gtin gtin,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(gtin);

        var row = await executor.QuerySingleOrDefaultAsync<ProductRow>(
            $"""
            select {Columns}
            from household_products
            where household_id = any(@library) and gtin = @gtin
            order by array_position(@library, household_id)
            limit 1;
            """,
            new { library = library.ToArray(), gtin = gtin.Value },
            cancellationToken).ConfigureAwait(false);

        return row?.ToStored();
    }

    public async Task<Result<long>> AddAsync(
        Guid householdId,
        Guid userId,
        HouseholdProduct product,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(product);

        var version = await executor.ExecuteScalarAsync<long?>(
            """
            insert into household_products
                (household_id, gtin, name, ingredient, quantity, unit, cook_minutes, note, created_by, updated_at)
            values
                (@householdId, @gtin, @name, @ingredient, @quantity, @unit, @cookMinutes, @note, @userId, @now)
            on conflict (household_id, gtin) do nothing
            returning version;
            """,
            Parameters(householdId, product, userId, expectedVersion: 0),
            cancellationToken).ConfigureAwait(false);

        return version is { } created ? created : ProductErrors.AlreadyNamed;
    }

    public async Task<Result<long>> UpdateAsync(
        Guid householdId,
        HouseholdProduct product,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(product);

        var version = await executor.ExecuteScalarAsync<long?>(
            """
            update household_products
            set name = @name, ingredient = @ingredient, quantity = @quantity, unit = @unit,
                cook_minutes = @cookMinutes, note = @note, updated_at = @now, version = version + 1
            where household_id = @householdId and gtin = @gtin and version = @expectedVersion
            returning version;
            """,
            Parameters(householdId, product, userId: null, expectedVersion),
            cancellationToken).ConfigureAwait(false);

        if (version is { } updated)
        {
            return updated;
        }

        // Only now, and only to say which failure it was: the version check itself lives in the update.
        return await ExistsAsync(householdId, product.Gtin, cancellationToken).ConfigureAwait(false)
            ? ConcurrencyErrors.VersionMismatch
            : ProductErrors.Unknown;
    }

    public async Task<Result> RemoveAsync(
        Guid householdId,
        Gtin gtin,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gtin);

        var removed = await executor.ExecuteAsync(
            """
            delete from household_products
            where household_id = @householdId and gtin = @gtin and version = @expectedVersion;
            """,
            new { householdId, gtin = gtin.Value, expectedVersion },
            cancellationToken).ConfigureAwait(false);

        // Already gone is fine; a row at another version is somebody else's change.
        return removed == 0 && await ExistsAsync(householdId, gtin, cancellationToken).ConfigureAwait(false)
            ? ConcurrencyErrors.VersionMismatch
            : Result.Success();
    }

    private async Task<bool> ExistsAsync(Guid householdId, Gtin gtin, CancellationToken cancellationToken) =>
        await executor.ExecuteScalarAsync<bool>(
            "select exists (select 1 from household_products where household_id = @householdId and gtin = @gtin);",
            new { householdId, gtin = gtin.Value },
            cancellationToken).ConfigureAwait(false);

    private object Parameters(Guid householdId, HouseholdProduct product, Guid? userId, long expectedVersion) => new
    {
        householdId,
        gtin = product.Gtin.Value,
        name = product.Name,
        ingredient = product.Ingredient,
        quantity = product.Quantity,
        unit = product.Unit,
        cookMinutes = product.CookMinutes,
        note = product.Note,
        userId,
        expectedVersion,
        now = time.GetUtcNow()
    };

    private sealed record ProductRow
    {
        public Guid HouseholdId { get; init; }

        public string Gtin { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string? Ingredient { get; init; }

        public decimal? Quantity { get; init; }

        public string? Unit { get; init; }

        public int? CookMinutes { get; init; }

        public string? Note { get; init; }

        public long Version { get; init; }

        // Stored rows were written from a valid product, so reading one back through the same rules cannot fail.
        internal StoredProduct ToStored() => new(
            HouseholdProduct.Create(Gtin, Name, Ingredient, Quantity, Unit, CookMinutes, Note).Match(
                product => product,
                error => throw new InvalidOperationException($"Stored product '{Gtin}' is not valid: {error.Code}.")),
            HouseholdId,
            Version);
    }
}
