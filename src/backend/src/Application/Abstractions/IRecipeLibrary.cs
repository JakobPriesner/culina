using Domain.Import;
using Domain.Shared;

namespace Application.Abstractions;

/// <summary>
/// Reads somebody else's recipe library.
/// </summary>
/// <remarks>
/// <para>
/// One implementation per app this can import from, and the only place that
/// knows what a Tandoor is. Everything above it works in
/// <see cref="SourceRecipe"/>, which is why adding Mealie is a class in
/// <c>Infrastructure</c> and a line in <see cref="IRecipeLibraries"/> rather
/// than a second import handler.
/// </para>
/// <para>
/// Three operations, and the split between the last two is the important one.
/// Browsing a library of two thousand recipes must not fetch two thousand
/// recipes — it asks for a page of names and pictures — and only the ones
/// somebody chose are fetched in full. An importer that fetched everything to
/// show a list would take ten minutes to draw its first screen.
/// </para>
/// <para>
/// Like <see cref="IWebPageFetcher"/>, every implementation of this makes the
/// server open a connection to an address a user chose, so every implementation
/// is a security control as well as a client.
/// </para>
/// </remarks>
public interface IRecipeLibrary
{
    /// <summary>Which app this reads.</summary>
    SourceKind Kind { get; }

    /// <summary>
    /// Trades a person's own sign-in for a token, where the app allows it.
    /// </summary>
    /// <param name="address">Which instance.</param>
    /// <param name="username">Their name over there.</param>
    /// <param name="password">Their password over there.</param>
    /// <param name="cancellationToken">Cancels the exchange.</param>
    /// <returns>The token to store, or why it could not be had.</returns>
    /// <remarks>
    /// <para>
    /// Here because "make an API token first" is the step that stops people
    /// moving their recipes at all: it is a thing they have to go and learn
    /// before they can start. Every app worth connecting to has some way to
    /// turn a sign-in into a token, and this is it.
    /// </para>
    /// <para>
    /// The password is used once and returned to nobody. It is never stored,
    /// never logged, and never leaves the call — what is kept is the token that
    /// comes back, which is the same token the person would have made by hand.
    /// </para>
    /// <para>
    /// An app with no such endpoint — or an instance whose accounts are all
    /// single sign-on, where there is no password to give — returns
    /// <see cref="ImportErrors.SignInNotPossible"/>, and the person pastes a
    /// token instead. That path never goes away for exactly this reason.
    /// </para>
    /// </remarks>
    Task<Result<string>> SignInAsync(
        SourceAddress address,
        string username,
        string password,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks that the address and token actually work.
    /// </summary>
    /// <param name="source">The connection to test.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <remarks>
    /// Called before a connection is saved, never after. A connection that is
    /// stored first and tested later is a row that looks fine on the settings
    /// screen and fails the first time somebody tries to use it — and by then
    /// they have forgotten what they typed.
    /// </remarks>
    Task<Result> TestAsync(RecipeSource source, CancellationToken cancellationToken);

    /// <summary>Reads a page of the library: enough to recognise and choose from.</summary>
    /// <param name="source">Which connection.</param>
    /// <param name="page">The page token from the previous read, or null to start.</param>
    /// <param name="query">What to search for over there, when somebody typed something.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<Result<SourcePage>> BrowseAsync(
        RecipeSource source,
        string? page,
        string? query,
        CancellationToken cancellationToken);

    /// <summary>Reads one recipe in full.</summary>
    /// <param name="source">Which connection.</param>
    /// <param name="externalId">Which recipe, as the other app identifies it.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    Task<Result<SourceRecipe>> FetchAsync(
        RecipeSource source,
        string externalId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Picks the library reader for a kind of app.
/// </summary>
/// <remarks>
/// A registry rather than a switch inside each handler, so the handlers stay
/// free of the list and adding a source touches one file. Explicit, like every
/// other registration here: a reader that is written but never registered fails
/// at the seam with a named error, not as a null somewhere deeper.
/// </remarks>
public interface IRecipeLibraries
{
    /// <summary>The reader for this kind, or a failure naming the kind.</summary>
    /// <param name="kind">Which app.</param>
    Result<IRecipeLibrary> For(SourceKind kind);
}
