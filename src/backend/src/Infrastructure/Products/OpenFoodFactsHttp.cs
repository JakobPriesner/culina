using System.Globalization;
using System.Net;
using System.Text.Json;
using Domain.Nutrition;
using Domain.Products;
using Domain.Shared;

namespace Infrastructure.Products;

/// <summary>
/// The two requests Culina makes to Open Food Facts: one product by barcode, and one name search. Kept under its
/// published limits (15 product reads and 10 searches a minute per IP) by counting here, in the one process that asks.
/// </summary>
/// <remarks>
/// A singleton: the limits are the server's, whoever is asking. Tests hand in an <see cref="HttpClient"/> over a fake
/// handler; no test reaches the real Open Food Facts.
/// </remarks>
internal sealed class OpenFoodFactsHttp(HttpClient client, TimeProvider time) : IDisposable
{
    /// <summary>Where Open Food Facts answers.</summary>
    internal static readonly Uri Address = new("https://world.openfoodfacts.org/");

    /// <summary>
    /// Its full-text search (search-a-licious), which ranks by relevance; the older <c>cgi/search.pl</c> put "Fromage
    /// Blanc" before every Skyr for "skyr" when this was built (27 October 2026).
    /// </summary>
    internal static readonly Uri SearchAddress = new("https://search.openfoodfacts.org/search");

    /// <summary>The fields Culina reads, so the answer is a few hundred bytes instead of the whole product document.</summary>
    private const string Fields =
        "code,product_name,product_name_de,brands,quantity,product_quantity,product_quantity_unit,serving_quantity,nutriments";

    /// <summary>The most a product or a page of results may be: the fields above are far below it.</summary>
    private const int MaxBytes = 512 * 1024;

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    // A little under the published limits, so a burst from another app on the same IP still leaves room.
    private readonly Limit products = new(12, time);
    private readonly Limit searches = new(8, time);

    /// <summary>The client Culina uses in production: no redirects followed, a short deadline.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The client owns its handler and this class disposes the client.")]
    internal static OpenFoodFactsHttp Create(TimeProvider time) =>
        new(
            new HttpClient(
                new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    AutomaticDecompression = DecompressionMethods.All,
                    ConnectTimeout = TimeSpan.FromSeconds(5)
                },
                disposeHandler: true)
            {
                BaseAddress = Address,
                Timeout = TimeSpan.FromSeconds(10)
            },
            time);

    /// <summary>One product: found, not known (a success with null), or why it could not be asked.</summary>
    /// <param name="gtin">The barcode.</param>
    /// <param name="contact">How to reach whoever runs this server, for the User-Agent.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    internal async Task<Result<Answer>> ProductAsync(Gtin gtin, string contact, CancellationToken cancellationToken)
    {
        if (!products.TryTake())
        {
            return ProductErrors.CatalogueBusy;
        }

        return await GetAsync(
            $"api/v2/product/{gtin.Value}?fields={Fields}",
            contact,
            root => new Answer(
                root is { } found && found.TryGetProperty("product", out var product) ? Read(product, gtin) : null),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Products sold in Germany whose name fits, best fit first.</summary>
    /// <param name="query">What was typed.</param>
    /// <param name="contact">How to reach whoever runs this server, for the User-Agent.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    internal async Task<Result<IReadOnlyList<CatalogueProduct>>> SearchAsync(
        string query,
        string contact,
        CancellationToken cancellationToken)
    {
        if (!searches.TryTake())
        {
            return ProductErrors.CatalogueBusy;
        }

        // Only the words: the search reads Lucene syntax, and a typed quote or colon must not become a filter.
        var words = string.Concat(query.Select(letter => char.IsLetterOrDigit(letter) ? letter : ' ')).Trim();
        var address = $"{SearchAddress}?page_size=20&langs=de&fields={Fields}&q="
                      + Uri.EscapeDataString($"{words} countries_tags:\"en:germany\"");

        return await GetAsync<IReadOnlyList<CatalogueProduct>>(
            address,
            contact,
            root => root is { } found &&
                    found.TryGetProperty("hits", out var list) &&
                    list.ValueKind == JsonValueKind.Array
                ? [.. list.EnumerateArray()
                    .Select(product => Gtin.Create(Text(product, "code")).Match(gtin => Read(product, gtin), _ => null))
                    .OfType<CatalogueProduct>()
                    .DistinctBy(product => product.Gtin.Value)]
                : [],
            cancellationToken).ConfigureAwait(false);
    }

    public void Dispose() => client.Dispose();

    /// <summary>A product, or null where Open Food Facts has none (or one without a name, which is as good as none).</summary>
    internal sealed record Answer(CatalogueProduct? Product);

    /// <summary>What <paramref name="read"/> makes of the answer (null for "no such product"), or why there is none.</summary>
    private async Task<Result<T>> GetAsync<T>(
        string path,
        string contact,
        Func<JsonElement?, T> read,
        CancellationToken cancellationToken)
        where T : notnull
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        // Open Food Facts asks every app to name itself and say how to reach whoever runs it.
        request.Headers.UserAgent.ParseAdd($"Culina/1.0 ({contact})");
        request.Headers.Accept.ParseAdd("application/json");

        try
        {
            using var response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return read(null);
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return ProductErrors.CatalogueBusy;
            }

            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxBytes)
            {
                return ProductErrors.CatalogueUnavailable;
            }

            var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            await using (body.ConfigureAwait(false))
            {
                using var buffer = new MemoryStream();

                if (!await BoundedRead.CopyAsync(body, buffer, MaxBytes, cancellationToken).ConfigureAwait(false))
                {
                    return ProductErrors.CatalogueUnavailable;
                }

                using var document = JsonDocument.Parse(buffer.ToArray());
                var root = document.RootElement;

                // "status": 0 is Open Food Facts' own "product not found", answered with 200 by older versions.
                return root.ValueKind == JsonValueKind.Object &&
                       root.TryGetProperty("status", out var status) &&
                       status.ValueKind == JsonValueKind.Number && status.GetInt32() == 0
                    ? read(null)
                    : read(root);
            }
        }
        catch (Exception failure) when (failure is HttpRequestException or IOException or JsonException
                                            or TaskCanceledException { CancellationToken.IsCancellationRequested: false })
        {
            return ProductErrors.CatalogueUnavailable;
        }
    }

    /// <summary>The product as Culina keeps it; null without a name, which a person could not recognise.</summary>
    private static CatalogueProduct? Read(JsonElement product, Gtin gtin)
    {
        if (product.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var name = Text(product, "product_name_de") ?? Text(product, "product_name");

        if (name is null)
        {
            return null;
        }

        var nutriments = product.TryGetProperty("nutriments", out var found) && found.ValueKind == JsonValueKind.Object
            ? found
            : default;
        var unit = Text(product, "product_quantity_unit")?.ToLowerInvariant();
        var packUnit = unit is "g" or "ml" ? unit : null;

        return new CatalogueProduct(
            gtin,
            Trimmed(name, 120),
            Brand(product) is { } brand ? Trimmed(brand, 120) : null,
            Text(product, "quantity") is { } quantity ? Trimmed(quantity, 60) : null,
            packUnit is null ? null : Positive(Number(product, "product_quantity")),
            packUnit,
            Positive(Number(product, "serving_quantity")),
            new Nutrients(
                Value(nutriments, "energy-kj_100g"),
                Value(nutriments, "energy-kcal_100g"),
                Value(nutriments, "fat_100g"),
                Value(nutriments, "saturated-fat_100g"),
                Value(nutriments, "carbohydrates_100g"),
                Value(nutriments, "sugars_100g"),
                Value(nutriments, "proteins_100g"),
                Value(nutriments, "salt_100g")));
    }

    // A product's page writes its brands as one string, the search as a list; the first is the one on the pack.
    private static string? Brand(JsonElement product)
    {
        var brands = product.TryGetProperty("brands", out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(one => one.ValueKind == JsonValueKind.String ? one.GetString() : null).FirstOrDefault()
            : Text(product, "brands")?.Split(',')[0];

        return brands?.Trim() is { Length: > 0 } brand ? brand : null;
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String &&
        value.GetString()?.Trim() is { Length: > 0 } text
            ? text
            : null;

    // Open Food Facts writes numbers as numbers or as strings, depending on how they were entered.
    private static decimal? Number(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(
                value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    // A label value: never negative, and rounded the way a label prints it.
    private static decimal? Value(JsonElement nutriments, string name) =>
        Number(nutriments, name) is { } value and >= 0 ? Math.Round(value, 2) : null;

    private static decimal? Positive(decimal? value) => value is > 0 and < 1_000_000 ? Math.Round(value.Value, 1) : null;

    private static string Trimmed(string text, int longest) => text.Length <= longest ? text : text[..longest].TrimEnd();

    /// <summary>At most so many requests within any minute.</summary>
    private sealed class Limit(int most, TimeProvider time)
    {
        private readonly Queue<DateTimeOffset> taken = new();
        private readonly Lock gate = new();

        internal bool TryTake()
        {
            var now = time.GetUtcNow();

            lock (gate)
            {
                while (taken.TryPeek(out var first) && now - first >= Window)
                {
                    taken.Dequeue();
                }

                if (taken.Count >= most)
                {
                    return false;
                }

                taken.Enqueue(now);

                return true;
            }
        }
    }
}
