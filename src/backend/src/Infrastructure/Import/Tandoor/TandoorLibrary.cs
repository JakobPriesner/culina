using System.Globalization;
using System.Net.Http.Headers;
using Application.Abstractions;
using Domain.Import;
using Domain.Shared;

namespace Infrastructure.Import.Tandoor;

/// <summary>Reads a Tandoor instance; every fact about that app stays in this folder.</summary>
/// <remarks>
/// Tries <c>Bearer</c> then DRF's older <c>Token</c> scheme. Paging is a whole URL in <c>next</c>, carried as an opaque token and
/// validated against the connection's origin before it is followed, because a round-tripped page token is user input.
/// </remarks>
internal sealed class TandoorLibrary(SourceHttp http) : IRecipeLibrary
{
    /// <summary>How many summaries one browse asks for: Tandoor's maximum, to minimise round trips on large libraries.</summary>
    private const int PageSize = 100;

    public SourceKind Kind => SourceKind.Tandoor;

    /// <summary>Trades a Tandoor sign-in for a Tandoor token, the same one the person would make under Settings.</summary>
    public async Task<Result<string>> SignInAsync(
        SourceAddress address,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        var answered = await http
            .PostFormAsync<TandoorToken>(
                address.At("/api-token-auth/"),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["username"] = username,
                    ["password"] = password
                },
                cancellationToken)
            .ConfigureAwait(false);

        return answered.Bind(token => string.IsNullOrWhiteSpace(token.Token)
            // Answered with no token: an all-single-sign-on instance lands here, and the answer is to paste one.
            ? Result<string>.Failure(ImportErrors.SignInNotPossible)
            : Result<string>.Success(token.Token.Trim()));
    }

    public async Task<Result> TestAsync(RecipeSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        // One recipe, not none: some versions reject page_size=0, and this must prove the recipe endpoint works.
        var probe = source.Address.At("/api/recipe/?page=1&page_size=1");

        var read = await ReadAsync<TandoorPage<TandoorRecipeSummary>>(source, probe, cancellationToken)
            .ConfigureAwait(false);

        return read.Match(_ => Result.Success(), Result.Failure);
    }

    public async Task<Result<SourcePage>> BrowseAsync(
        RecipeSource source,
        string? page,
        string? query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        var url = Next(source, page) ?? First(source, query);

        var read = await ReadAsync<TandoorPage<TandoorRecipeSummary>>(source, url, cancellationToken)
            .ConfigureAwait(false);

        return read.Map(answered => new SourcePage(
            [.. (answered.Results ?? []).Select(TandoorMapping.ToSource)],
            answered.Next,
            answered.Count));
    }

    public async Task<Result<SourceRecipe>> FetchAsync(
        RecipeSource source,
        string externalId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Tandoor ids are integers and only this class knows it; anything else never reaches a URL.
        if (!int.TryParse(externalId, CultureInfo.InvariantCulture, out var id) || id <= 0)
        {
            return ImportErrors.SourceNotUnderstood;
        }

        var url = source.Address.At($"/api/recipe/{id.ToString(CultureInfo.InvariantCulture)}/");

        var read = await ReadAsync<TandoorRecipe>(source, url, cancellationToken).ConfigureAwait(false);

        return read.Map(TandoorMapping.ToSource);
    }

    /// <summary>Reads a recipe's picture. A path is resolved against the connection; a whole address is followed only on the same origin, as it came from a response.</summary>
    public async Task<Result<Stream>> FetchPictureAsync(
        RecipeSource source,
        string pictureUrl,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (OnTheSameServer(source, pictureUrl) is not { } url)
        {
            return ImportErrors.UnreachableAddress;
        }

        // The token goes along: media is often behind the API's sign-in, and this only ever asks the token's own server.
        return await http
            .GetPictureAsync(url, new AuthenticationHeaderValue("Bearer", source.Secret), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>The picture's address, only if on the connection's own server. Internal so the security rule is tested by name.</summary>
    internal static Uri? OnTheSameServer(RecipeSource source, string pictureUrl)
    {
        var written = pictureUrl?.Trim();

        if (string.IsNullOrEmpty(written))
        {
            return null;
        }

        if (!Uri.TryCreate(source.Address.Origin, written, out var url))
        {
            return null;
        }

        return url.GetLeftPart(UriPartial.Authority) == source.Address.Value ? url : null;
    }

    private static Uri First(RecipeSource source, string? query)
    {
        var search = string.IsNullOrWhiteSpace(query)
            ? string.Empty
            : $"&query={Uri.EscapeDataString(query.Trim())}";

        return source.Address.At(
            $"/api/recipe/?page=1&page_size={PageSize.ToString(CultureInfo.InvariantCulture)}{search}");
    }

    // The page token, once proved to point at this instance: following it unchecked would let a caller name the address
    // fetched with the household's token attached. Foreign origins are ignored and the browse starts over.
    private static Uri? Next(RecipeSource source, string? page)
    {
        if (string.IsNullOrWhiteSpace(page)
            || !Uri.TryCreate(page.Trim(), UriKind.Absolute, out var url))
        {
            return null;
        }

        return url.GetLeftPart(UriPartial.Authority) == source.Address.Value ? url : null;
    }

    // Tries the current token scheme, then the older one, only on a refusal and only once.
    private async Task<Result<TBody>> ReadAsync<TBody>(
        RecipeSource source,
        Uri url,
        CancellationToken cancellationToken)
        where TBody : notnull
    {
        var bearer = await http
            .GetAsync<TBody>(url, new AuthenticationHeaderValue("Bearer", source.Secret), cancellationToken)
            .ConfigureAwait(false);

        var refused = bearer.Match(_ => false, error => error == ImportErrors.SourceRefused);

        if (!refused)
        {
            return bearer;
        }

        return await http
            .GetAsync<TBody>(url, new AuthenticationHeaderValue("Token", source.Secret), cancellationToken)
            .ConfigureAwait(false);
    }
}
