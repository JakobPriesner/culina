using System.Globalization;
using System.Net.Http.Headers;
using Application.Abstractions;
using Domain.Import;
using Domain.Shared;

namespace Infrastructure.Import.Tandoor;

/// <summary>
/// Reads a Tandoor instance.
/// </summary>
/// <remarks>
/// <para>
/// The first implementation of <see cref="IRecipeLibrary"/>, and the shape the
/// next one follows: talk to one app, answer in <see cref="SourceRecipe"/>, and
/// keep every fact about that app inside this folder.
/// </para>
/// <para>
/// Two accommodations for reality. Tandoor changed its token scheme —
/// <c>Bearer</c> on current versions, DRF's <c>Token</c> on older ones — and
/// somebody moving out of an instance they set up in 2021 is exactly the person
/// this feature is for, so both are tried. And its paging is a whole URL in a
/// <c>next</c> field rather than a page number, which is carried through as an
/// opaque token and validated against the connection's own address before it is
/// followed: a page token is user input once it has been round-tripped through
/// a client.
/// </para>
/// </remarks>
internal sealed class TandoorLibrary(SourceHttp http) : IRecipeLibrary
{
    /// <summary>
    /// How many summaries one browse asks for.
    /// </summary>
    /// <remarks>
    /// Tandoor's recipe list refuses more than a hundred, so this is as much as
    /// it will give. A summary is a name and a couple of numbers, so a hundred
    /// of them is a small answer — and the number that matters is how many
    /// round trips "select all" costs on a library of two thousand: twenty
    /// rather than the fifty-six a screenful-sized page would take.
    /// </remarks>
    private const int PageSize = 100;

    public SourceKind Kind => SourceKind.Tandoor;

    /// <summary>
    /// Trades a Tandoor sign-in for a Tandoor token.
    /// </summary>
    /// <remarks>
    /// <c>/api-token-auth/</c> is Django REST Framework's obtain-token view,
    /// which Tandoor keeps. It answers with an existing read-write token when
    /// the account already has one and mints a long-lived one otherwise, so
    /// this is the same token the person would have made by hand under
    /// Settings — not a second credential to keep track of.
    /// </remarks>
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
            // It answered, and with something that is not a token. An instance
            // whose accounts are all single sign-on lands here, and the answer
            // is to paste one rather than to fix anything.
            ? Result<string>.Failure(ImportErrors.SignInNotPossible)
            : Result<string>.Success(token.Token.Trim()));
    }

    public async Task<Result> TestAsync(RecipeSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        // One recipe, not none: a page size of zero is a request some versions
        // answer and others reject, and this has to prove the recipe endpoint
        // works rather than that something answered.
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

        // Tandoor identifies recipes by integer, and this is the only place
        // that knows it. An id that is not one never becomes part of a URL.
        if (!int.TryParse(externalId, CultureInfo.InvariantCulture, out var id) || id <= 0)
        {
            return ImportErrors.SourceNotUnderstood;
        }

        var url = source.Address.At($"/api/recipe/{id.ToString(CultureInfo.InvariantCulture)}/");

        var read = await ReadAsync<TandoorRecipe>(source, url, cancellationToken).ConfigureAwait(false);

        return read.Map(TandoorMapping.ToSource);
    }

    /// <summary>
    /// Reads a recipe's picture from the instance it came from.
    /// </summary>
    /// <remarks>
    /// Tandoor writes this field either as a path on itself — the usual case,
    /// its media directory — or as a whole address, which is what an instance
    /// keeping its media elsewhere produces. A path is resolved against the
    /// connection; an address is followed only if it lands back on the same
    /// origin, because this string came out of a response rather than out of a
    /// person, and following it anywhere would let the answer choose what this
    /// server connects to.
    /// </remarks>
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

        // The token goes with it. Tandoor's media is often behind the same
        // sign-in as its API, and sending it is safe precisely because this
        // only ever asks the server the token belongs to.
        return await http
            .GetPictureAsync(url, new AuthenticationHeaderValue("Bearer", source.Secret), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// The picture's address, but only if it is on the connection's own server.
    /// </summary>
    /// <remarks>
    /// Internal rather than private so it can be tested directly. It is a
    /// security rule with a handful of cases — a path, a whole address on the
    /// same host, one on another host, a protocol-relative one — and a rule
    /// nothing exercises by name is one that quietly stops holding.
    /// </remarks>
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

    /// <summary>
    /// The page token, once it has been proved to point at this instance.
    /// </summary>
    /// <remarks>
    /// Tandoor hands back a whole URL, and it goes out to a client and comes
    /// back. Following it unchecked would be letting a caller name the address
    /// the server fetches — with the household's token attached — which is the
    /// one thing this whole feature is careful about. Anything not on the
    /// connection's own origin is ignored, and the browse starts over.
    /// </remarks>
    private static Uri? Next(RecipeSource source, string? page)
    {
        if (string.IsNullOrWhiteSpace(page)
            || !Uri.TryCreate(page.Trim(), UriKind.Absolute, out var url))
        {
            return null;
        }

        return url.GetLeftPart(UriPartial.Authority) == source.Address.Value ? url : null;
    }

    /// <summary>
    /// Reads, trying the current token scheme and then the older one.
    /// </summary>
    /// <remarks>
    /// Only on a refusal, and only once. A wrong token fails twice and reports
    /// the same thing it would have reported after one attempt; a correct token
    /// on an old instance works, where it would otherwise have looked wrong.
    /// </remarks>
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
