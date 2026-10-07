using Domain.Import;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Reads somebody else's recipe library.</summary>
/// <remarks>
/// One implementation per app, and the only place that knows what a Tandoor is. Like
/// <see cref="IWebPageFetcher"/>, each connects to a user-chosen address, so each is a security control.
/// </remarks>
public interface IRecipeLibrary
{
    /// <summary>Which app this reads.</summary>
    SourceKind Kind { get; }

    /// <summary>Trades a person's own sign-in for a token, where the app allows it.</summary>
    /// <returns>The token to store, or why it could not be had.</returns>
    /// <remarks>
    /// The password is used once: never stored or logged. Returns <see cref="ImportErrors.SignInNotPossible"/>
    /// for apps without such an endpoint or with SSO-only accounts; pasting a token remains the fallback.
    /// </remarks>
    Task<Result<string>> SignInAsync(
        SourceAddress address,
        string username,
        string password,
        CancellationToken cancellationToken);

    /// <summary>Checks that the address and token work. Called before a connection is saved.</summary>
    Task<Result> TestAsync(RecipeSource source, CancellationToken cancellationToken);

    /// <summary>Reads a page of the library: enough to recognise and choose from.</summary>
    Task<Result<SourcePage>> BrowseAsync(
        RecipeSource source,
        string? page,
        string? query,
        CancellationToken cancellationToken);

    /// <summary>Reads a recipe's picture, when it has one that may be fetched.</summary>
    /// <returns>The bytes, for the image store to decode; the caller disposes them.</returns>
    /// <remarks>
    /// Best effort. The picture address is data from the other server, so an implementation must refuse
    /// anything off the connection's own origin, otherwise that server could choose what this one fetches.
    /// </remarks>
    Task<Result<Stream>> FetchPictureAsync(
        RecipeSource source,
        string pictureUrl,
        CancellationToken cancellationToken);

    /// <summary>Reads one recipe in full.</summary>
    Task<Result<SourceRecipe>> FetchAsync(
        RecipeSource source,
        string externalId,
        CancellationToken cancellationToken);
}

/// <summary>Picks the library reader for a kind of app.</summary>
public interface IRecipeLibraries
{
    /// <summary>The reader for this kind, or a failure naming the kind.</summary>
    Result<IRecipeLibrary> For(SourceKind kind);
}
