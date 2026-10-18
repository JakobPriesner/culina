using System.Globalization;
using System.Net.Http.Headers;
using Application.Abstractions;
using Domain.Import;
using Domain.Shared;

namespace Infrastructure.Import.Culina;

/// <summary>Reads another Culina instance; every fact about its API stays in this folder.</summary>
/// <remarks>
/// Recipes belong to households there too, so a browse walks every kitchen the person belongs to, one after the
/// other, rather than asking which one when connecting. The page token is this class's own
/// <c>{kitchen}</c> or <c>{kitchen}.{cursor}</c>: the kitchen is a position in the person's households sorted by
/// id, and the cursor is the other instance's, sent back only as an escaped query value on its own address.
/// </remarks>
internal sealed class CulinaLibrary(SourceHttp http) : IRecipeLibrary
{
    /// <summary>The most the other instance's recipe list gives at once.</summary>
    private const int PageSize = 100;

    public SourceKind Kind => SourceKind.Culina;

    /// <summary>Signs in as client <c>instance</c>, which is answered with a bearer token instead of a cookie.</summary>
    /// <remarks>
    /// The token is an ordinary session there: it shows on the person's devices screen and they can revoke it.
    /// It ends as any session does, after a long spell unused; signing in again makes a new one.
    /// </remarks>
    public async Task<Result<string>> SignInAsync(
        SourceAddress address,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        var session = await http
            .PostJsonAsync<CulinaSession>(
                address.At("/api/v1/sessions"),
                new { email = username, password, client = "instance" },
                authorization: null,
                cancellationToken)
            .ConfigureAwait(false);

        // Answered with no token: an instance from before client "instance" set a cookie instead. Pasting a token is
        // no way round it, since Culina has none to paste.
        return session.Bind(answered => string.IsNullOrWhiteSpace(answered.SessionToken)
            ? Result<string>.Failure(ImportErrors.SourceTooOld)
            : Result<string>.Success(answered.SessionToken.Trim()));
    }

    public async Task<Result> TestAsync(RecipeSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        var read = await KitchensAsync(source, cancellationToken).ConfigureAwait(false);

        return await read.Match(
                kitchens => TestAsync(source, kitchens, cancellationToken),
                error => Task.FromResult(Result.Failure(error)))
            .ConfigureAwait(false);
    }

    public async Task<Result<SourcePage>> BrowseAsync(
        RecipeSource source,
        string? page,
        string? query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        var kitchens = await KitchensAsync(source, cancellationToken).ConfigureAwait(false);

        return await kitchens.Match(
                all => BrowseAsync(source, all, page, query, cancellationToken),
                error => Task.FromResult(Result<SourcePage>.Failure(error)))
            .ConfigureAwait(false);
    }

    public async Task<Result<SourceRecipe>> FetchAsync(
        RecipeSource source,
        string externalId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Recipe ids are GUIDs and only this class knows it; anything else never reaches a URL.
        if (!Guid.TryParseExact(externalId, "D", out var id))
        {
            return ImportErrors.SourceNotUnderstood;
        }

        var read = await http
            .GetAsync<CulinaRecipe>(source.Address.At($"/api/v1/recipes/{id:D}"), Bearer(source), cancellationToken)
            .ConfigureAwait(false);

        return read.Map(CulinaMapping.ToSource);
    }

    /// <summary>Reads a recipe's picture, only from the connection's own server.</summary>
    public async Task<Result<Stream>> FetchPictureAsync(
        RecipeSource source,
        string pictureUrl,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (SourceOrigin.Resolve(source.Address, pictureUrl) is not { } url)
        {
            return ImportErrors.UnreachableAddress;
        }

        // Pictures are private there, so the token goes along.
        return await http.GetPictureAsync(url, Bearer(source), cancellationToken).ConfigureAwait(false);
    }

    // One recipe, not none: this must prove the recipe list answers to this token, not only the household list.
    private async Task<Result> TestAsync(
        RecipeSource source,
        IReadOnlyList<Guid> kitchens,
        CancellationToken cancellationToken)
    {
        // A person in no kitchen has nothing to bring over, but the connection works.
        if (kitchens is not [var first, ..])
        {
            return Result.Success();
        }

        var page = await ReadPageAsync(source, first, cursor: null, query: null, limit: 1, cancellationToken)
            .ConfigureAwait(false);

        return page.Match(_ => Result.Success(), Result.Failure);
    }

    private async Task<Result<SourcePage>> BrowseAsync(
        RecipeSource source,
        IReadOnlyList<Guid> kitchens,
        string? page,
        string? query,
        CancellationToken cancellationToken)
    {
        // Anything that does not read as one of this class's tokens starts the browse over.
        var (kitchen, cursor) = ReadToken(page, kitchens.Count);

        if (kitchens.Count == 0)
        {
            return new SourcePage([], null, 0);
        }

        var read = await ReadPageAsync(source, kitchens[kitchen], cursor, query, PageSize, cancellationToken)
            .ConfigureAwait(false);

        return read.Map(answered =>
        {
            var next = answered.NextCursor is { Length: > 0 } more
                ? Token(kitchen, more)
                : kitchen + 1 < kitchens.Count ? Token(kitchen + 1, null) : null;

            // A recipe another kitchen lends is that kitchen's to give, and appears there if the person is in it.
            var recipes = (answered.Items ?? [])
                .Where(summary => summary.RecipeId is not null && summary.HouseholdId == kitchens[kitchen])
                .Select(CulinaMapping.ToSource)
                .ToList();

            // A sum over several kitchens would cost a read of each; one kitchen's own count is exact.
            return new SourcePage(recipes, next, kitchens.Count == 1 ? answered.Total : null);
        });
    }

    // Sorted by id so a page token means the same kitchen on the next read; a kitchen in the bin is passed over.
    private async Task<Result<IReadOnlyList<Guid>>> KitchensAsync(
        RecipeSource source,
        CancellationToken cancellationToken)
    {
        var read = await http
            .GetAsync<CulinaHouseholds>(source.Address.At("/api/v1/households"), Bearer(source), cancellationToken)
            .ConfigureAwait(false);

        return read.Map(answered => (IReadOnlyList<Guid>)
        [
            .. (answered.Items ?? [])
                .Where(household => household.DeletedAt is null)
                .Select(household => household.HouseholdId)
                .OfType<Guid>()
                .Order()
        ]);
    }

    private Task<Result<CulinaRecipePage>> ReadPageAsync(
        RecipeSource source,
        Guid kitchen,
        string? cursor,
        string? query,
        int limit,
        CancellationToken cancellationToken)
    {
        var path = string.Create(
            CultureInfo.InvariantCulture,
            $"/api/v1/recipes?householdId={kitchen:D}&sort=title&limit={limit}");

        if (!string.IsNullOrWhiteSpace(query))
        {
            path += $"&query={Uri.EscapeDataString(query.Trim())}&asTyped=true";
        }

        if (cursor is not null)
        {
            path += $"&cursor={Uri.EscapeDataString(cursor)}";
        }

        return http.GetAsync<CulinaRecipePage>(source.Address.At(path), Bearer(source), cancellationToken);
    }

    private static string Token(int kitchen, string? cursor) =>
        cursor is null
            ? kitchen.ToString(CultureInfo.InvariantCulture)
            : string.Create(CultureInfo.InvariantCulture, $"{kitchen}.{cursor}");

    private static (int Kitchen, string? Cursor) ReadToken(string? page, int kitchens)
    {
        var parts = page?.Split('.', 2) ?? [];

        return parts is [var number, ..]
            && int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var kitchen)
            && kitchen < kitchens
                ? (kitchen, parts is [_, { Length: > 0 } cursor] ? cursor : null)
                : (0, null);
    }

    private static AuthenticationHeaderValue Bearer(RecipeSource source) => new("Bearer", source.Secret);
}
