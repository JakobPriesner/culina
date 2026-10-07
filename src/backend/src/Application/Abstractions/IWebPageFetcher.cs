using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Fetches a public web page, and refuses to fetch anything else.</summary>
/// <remarks>Its implementation is a security control: the server opens a connection to a user-chosen address from inside its own network.</remarks>
public interface IWebPageFetcher
{
    /// <summary>Reads a page, or says why it would not.</summary>
    /// <param name="url">The address a person pasted, already parsed.</param>
    /// <param name="cancellationToken">Cancels the fetch.</param>
    /// <remarks>The scheme is still checked here: <c>file:///etc/passwd</c> parses as a <see cref="Uri"/>.</remarks>
    Task<Result<WebPage>> FetchAsync(Uri url, CancellationToken cancellationToken);
}

/// <summary>A page that was fetched.</summary>
/// <param name="Url">Where it was finally read from, after any redirects.</param>
/// <param name="Html">Its markup, capped.</param>
public sealed record WebPage(Uri Url, string Html);
