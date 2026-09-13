using Domain.Shared;

namespace Application.Abstractions;

/// <summary>
/// Fetches a public web page, and refuses to fetch anything else.
/// </summary>
/// <remarks>
/// The one abstraction in this codebase whose implementation is a security
/// control rather than a detail. Importing a recipe means the <em>server</em>
/// opens a connection to an address a user chose, from inside whatever network
/// it is deployed in — so what this must not do is as important as what it does.
/// </remarks>
public interface IWebPageFetcher
{
    /// <summary>Reads a page, or says why it would not.</summary>
    /// <param name="url">The address a person pasted, already parsed.</param>
    /// <param name="cancellationToken">Cancels the fetch.</param>
    /// <remarks>
    /// A <see cref="Uri"/> rather than a string, but the scheme is checked here
    /// all the same: parsing proves only that it is an address, and
    /// <c>file:///etc/passwd</c> parses perfectly.
    /// </remarks>
    Task<Result<WebPage>> FetchAsync(Uri url, CancellationToken cancellationToken);
}

/// <summary>A page that was fetched.</summary>
/// <param name="Url">Where it was finally read from, after any redirects.</param>
/// <param name="Html">Its markup, capped.</param>
public sealed record WebPage(Uri Url, string Html);
