using System.Globalization;
using System.Net.Http.Headers;
using Application.Abstractions;
using Domain.Import;
using Domain.Shared;

namespace Infrastructure.Import.Mealie;

/// <summary>Reads a Mealie v2 instance; every fact about that app stays in this folder.</summary>
/// <remarks>
/// The page token is this class's own page number, never Mealie's <c>next</c> link, so no address from the other
/// server is ever followed while browsing. Pages are sorted by name so that one page leads on to the next.
/// </remarks>
internal sealed class MealieLibrary(SourceHttp http) : IRecipeLibrary
{
    /// <summary>How many summaries one browse asks for, matching what Tandoor gives.</summary>
    private const int PageSize = 100;

    /// <summary>What the token made at sign-in is called on the person's Mealie profile, so they can see and revoke it.</summary>
    private const string TokenName = "Culina";

    public SourceKind Kind => SourceKind.Mealie;

    /// <summary>Trades a Mealie sign-in for a long-lived Mealie token, the same one the person would make on their profile.</summary>
    /// <remarks>
    /// Mealie's sign-in answers with a session token that expires, which would leave a connection that stops
    /// working in two days; it is used once, to make the lasting one, and dropped.
    /// </remarks>
    public async Task<Result<string>> SignInAsync(
        SourceAddress address,
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        var session = await http
            .PostFormAsync<MealieSession>(
                address.At("/api/auth/token"),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["username"] = username,
                    ["password"] = password
                },
                cancellationToken)
            .ConfigureAwait(false);

        return await session
            .Match(
                answered => string.IsNullOrWhiteSpace(answered.AccessToken)
                    // Answered with no token: a single-sign-on-only instance lands here, and the answer is to paste one.
                    ? Task.FromResult(Result<string>.Failure(ImportErrors.SignInNotPossible))
                    : MintAsync(address, answered.AccessToken.Trim(), cancellationToken),
                error => Task.FromResult(Result<string>.Failure(error)))
            .ConfigureAwait(false);
    }

    public async Task<Result> TestAsync(RecipeSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        // One recipe, not none: this must prove the recipe endpoint works.
        var read = await ReadAsync<MealiePage<MealieRecipeSummary>>(
                source,
                source.Address.At("/api/recipes?page=1&perPage=1"),
                cancellationToken)
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

        // Anything that is not a page number starts the browse over, as a foreign page link does for Tandoor.
        var number = int.TryParse(page, NumberStyles.None, CultureInfo.InvariantCulture, out var asked) && asked > 0
            ? asked
            : 1;

        var read = await ReadAsync<MealiePage<MealieRecipeSummary>>(
                source,
                Page(source, number, query),
                cancellationToken)
            .ConfigureAwait(false);

        return read.Map(answered => new SourcePage(
            [.. (answered.Items ?? []).Where(summary => summary.Id is not null).Select(MealieMapping.ToSource)],
            number < answered.TotalPages ? (number + 1).ToString(CultureInfo.InvariantCulture) : null,
            answered.Total));
    }

    public async Task<Result<SourceRecipe>> FetchAsync(
        RecipeSource source,
        string externalId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Mealie ids are GUIDs and only this class knows it; anything else never reaches a URL.
        if (!Guid.TryParseExact(externalId, "D", out var id))
        {
            return ImportErrors.SourceNotUnderstood;
        }

        var read = await ReadAsync<MealieRecipe>(source, source.Address.At($"/api/recipes/{id:D}"), cancellationToken)
            .ConfigureAwait(false);

        return read.Map(MealieMapping.ToSource);
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

        // Mealie serves pictures without sign-in, but one behind a proxy that asks for it should still load.
        return await http
            .GetPictureAsync(url, Bearer(source.Secret), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Result<string>> MintAsync(
        SourceAddress address,
        string session,
        CancellationToken cancellationToken)
    {
        var minted = await http
            .PostJsonAsync<MealieApiToken>(
                address.At("/api/users/api-tokens"),
                new { name = TokenName },
                Bearer(session),
                cancellationToken)
            .ConfigureAwait(false);

        return minted.Bind(token => string.IsNullOrWhiteSpace(token.Token)
            ? Result<string>.Failure(ImportErrors.SignInNotPossible)
            : Result<string>.Success(token.Token.Trim()));
    }

    private static Uri Page(RecipeSource source, int number, string? query)
    {
        var search = string.IsNullOrWhiteSpace(query)
            ? string.Empty
            : $"&search={Uri.EscapeDataString(query.Trim())}";

        return source.Address.At(string.Create(
            CultureInfo.InvariantCulture,
            $"/api/recipes?page={number}&perPage={PageSize}&orderBy=name&orderDirection=asc{search}"));
    }

    private Task<Result<TBody>> ReadAsync<TBody>(RecipeSource source, Uri url, CancellationToken cancellationToken)
        where TBody : notnull =>
        http.GetAsync<TBody>(url, Bearer(source.Secret), cancellationToken);

    private static AuthenticationHeaderValue Bearer(string? token) => new("Bearer", token);
}
