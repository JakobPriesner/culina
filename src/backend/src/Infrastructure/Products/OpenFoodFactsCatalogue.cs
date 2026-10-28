using System.Text.Json;
using Application.Abstractions;
using Application.Abstractions.Settings;
using Domain.Nutrition;
using Domain.Products;
using Domain.Shared;
using Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Products;

/// <summary>
/// Open Food Facts behind the server's own memory of it (<c>product_lookups</c>): a product is asked again after 30
/// days, a code it does not know after 7, and an old answer is still given when it cannot be asked just now.
/// </summary>
internal sealed partial class OpenFoodFactsCatalogue(
    DbExecutor executor,
    OpenFoodFactsHttp http,
    ProductSettings settings,
    TimeProvider time,
    ILogger<OpenFoodFactsCatalogue> logger) : IProductCatalogue
{
    private static readonly TimeSpan KeepFound = TimeSpan.FromDays(30);
    private static readonly TimeSpan KeepMissing = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<CatalogueProduct>> FindAsync(Gtin gtin, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gtin);

        var kept = await ReadAsync(gtin, cancellationToken).ConfigureAwait(false);

        if (kept is not null && time.GetUtcNow() - kept.FetchedAt < (kept.Found ? KeepFound : KeepMissing))
        {
            return Answered(kept.ToProduct(gtin));
        }

        var asked = await http.ProductAsync(gtin, settings.ContactOrProject, cancellationToken).ConfigureAwait(false);

        return await asked.Match(
            async answer =>
            {
                await KeepAsync(gtin, answer.Product, cancellationToken).ConfigureAwait(false);

                return Answered(answer.Product);
            },
            error =>
            {
                LogNotAsked(logger, error.Code);

                // Better an answer from a few weeks ago than none while Open Food Facts is busy or away.
                return Task.FromResult(kept is null ? Result<CatalogueProduct>.Failure(error) : Answered(kept.ToProduct(gtin)));
            }).ConfigureAwait(false);
    }

    public async Task<CatalogueProduct?> RememberedAsync(Gtin gtin, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gtin);

        return (await ReadAsync(gtin, cancellationToken).ConfigureAwait(false))?.ToProduct(gtin);
    }

    public async Task<Result<IReadOnlyList<CatalogueProduct>>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var found = await http.SearchAsync(query, settings.ContactOrProject, cancellationToken).ConfigureAwait(false);

        // Not kept: the search's index lacks a pack's size, so choosing one asks for the whole product by its code.
        return found.Match(
            products => Result<IReadOnlyList<CatalogueProduct>>.Success(products),
            error =>
            {
                LogNotAsked(logger, error.Code);

                return Result<IReadOnlyList<CatalogueProduct>>.Failure(error);
            });
    }

    private static Result<CatalogueProduct> Answered(CatalogueProduct? product) =>
        product is null ? ProductErrors.NotInCatalogue : product;

    private Task<LookupRow?> ReadAsync(Gtin gtin, CancellationToken cancellationToken) =>
        executor.QuerySingleOrDefaultAsync<LookupRow>(
            "select found, product::text as product, fetched_at from product_lookups where gtin = @gtin;",
            new { gtin = gtin.Value },
            cancellationToken);

    private Task<int> KeepAsync(Gtin gtin, CatalogueProduct? product, CancellationToken cancellationToken) =>
        executor.ExecuteAsync(
            """
            insert into product_lookups (gtin, found, product, fetched_at)
            values (@gtin, @found, cast(@product as jsonb), @now)
            on conflict (gtin) do update
            set found = excluded.found, product = excluded.product, fetched_at = excluded.fetched_at;
            """,
            new
            {
                gtin = gtin.Value,
                found = product is not null,
                product = product is null ? null : JsonSerializer.Serialize(Stored.Of(product), Json),
                now = time.GetUtcNow()
            },
            cancellationToken);

    [LoggerMessage(
        EventId = 1980,
        Level = LogLevel.Warning,
        Message = "Open Food Facts could not be asked: {ErrorCode}")]
    private static partial void LogNotAsked(ILogger logger, string errorCode);

    private sealed record LookupRow
    {
        public bool Found { get; init; }

        public string? Product { get; init; }

        public DateTimeOffset FetchedAt { get; init; }

        internal CatalogueProduct? ToProduct(Gtin gtin) =>
            Found && Product is { } json ? JsonSerializer.Deserialize<Stored>(json, Json)?.ToProduct(gtin) : null;
    }

    /// <summary>The fields kept per product; the barcode is the row's key.</summary>
    private sealed record Stored(
        string Name,
        string? Brand,
        string? Quantity,
        decimal? PackGrams,
        string? PackUnit,
        decimal? ServingGrams,
        Nutrients Per100Grams)
    {
        internal static Stored Of(CatalogueProduct product) =>
            new(
                product.Name,
                product.Brand,
                product.Quantity,
                product.PackGrams,
                product.PackUnit,
                product.ServingGrams,
                product.Per100Grams);

        internal CatalogueProduct ToProduct(Gtin gtin) =>
            new(gtin, Name, Brand, Quantity, PackGrams, PackUnit, ServingGrams, Per100Grams);
    }
}
