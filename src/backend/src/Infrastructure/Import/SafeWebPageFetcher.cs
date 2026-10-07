using System.Net;
using Application.Abstractions;
using Domain.Import;
using Domain.Shared;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Import;

/// <summary>Fetches a public web page, and nothing else.</summary>
/// <remarks>
/// The server does the fetching, so: http(s) only; connect to the checked address, not the name
/// (DNS rebinding); follow redirects by hand, revalidating each; deadline on the whole exchange;
/// size cap enforced while reading. Failures are vague on purpose, or this becomes a port scanner.
/// </remarks>
internal sealed partial class SafeWebPageFetcher : IWebPageFetcher, IDisposable
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    private const int MaxBytes = 2 * 1024 * 1024;

    /// <summary>
    /// How many redirects are followed: enough for http to https to www, far short of a loop.
    /// </summary>
    private const int MaxHops = 5;

    private readonly SocketsHttpHandler handler;
    private readonly HttpClient client;
    private readonly ILogger<SafeWebPageFetcher> logger;

    public SafeWebPageFetcher(ILogger<SafeWebPageFetcher> logger)
    {
        this.logger = logger;

        // Never private, whatever the operator allows for a connected source: the address came from
        // a text box.
        handler = CheckedConnections.Handler(admits: PublicAddress.IsPublic);

        client = new HttpClient(handler, disposeHandler: false) { Timeout = Deadline };

        // Said plainly: pretending to be a browser would make it harder for a site to say no.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Culina/1.0 (self-hosted recipe import)");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html, application/xhtml+xml");
    }

    public async Task<Result<WebPage>> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);

        if (!IsFetchableScheme(url))
        {
            return ImportErrors.UnreachableAddress;
        }

        var target = url;

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        deadline.CancelAfter(Deadline);

        try
        {
            return await FollowAsync(target, deadline.Token).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is HttpRequestException or OperationCanceledException
                                            or InvalidOperationException or IOException)
        {
            // Logged with the host, never returned with it: the caller learns only that it failed.
            LogFetchFailed(logger, url.Host, failure);

            return ImportErrors.CouldNotFetch;
        }
    }

    private async Task<Result<WebPage>> FollowAsync(Uri target, CancellationToken cancellationToken)
    {
        var current = target;

        for (var hop = 0; hop <= MaxHops; hop += 1)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            using var response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            LogAnswered(logger, current.Host, (int)response.StatusCode);

            if (!IsRedirect(response.StatusCode))
            {
                return await ReadAsync(current, response, cancellationToken).ConfigureAwait(false);
            }

            if (response.Headers.Location is not { } location)
            {
                return ImportErrors.CouldNotFetch;
            }

            // Resolved against the current page: a relative Location would otherwise hit this
            // server.
            var next = new Uri(current, location);

            if (!IsFetchableScheme(next))
            {
                return ImportErrors.UnreachableAddress;
            }

            current = next;
        }

        return ImportErrors.CouldNotFetch;
    }

    private static async Task<Result<WebPage>> ReadAsync(
        Uri url,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            return ImportErrors.CouldNotFetch;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;

        if (mediaType is not ("text/html" or "application/xhtml+xml" or "text/plain"
            or "text/vtt" or "application/xml" or "text/xml" or "application/json"))
        {
            return ImportErrors.NotAWebPage;
        }

        // Checked before reading when the server says, and again while reading: Content-Length is a
        // claim.
        if (response.Content.Headers.ContentLength > MaxBytes)
        {
            return ImportErrors.TooLarge;
        }

        var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        await using (body.ConfigureAwait(false))
        {
            var buffer = new MemoryStream();
            var chunk = new byte[81_920];

            while (buffer.Length <= MaxBytes)
            {
                var read = await body.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);

                if (read == 0)
                {
                    return new WebPage(
                        url,
                        System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
                }

                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);
            }

            return ImportErrors.TooLarge;
        }
    }

    private static bool IsFetchableScheme(Uri url) =>
        url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps;

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found
            or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;

    public void Dispose()
    {
        client.Dispose();
        handler.Dispose();
    }

    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Information,
        Message = "Could not fetch {Host} for an import")]
    private static partial void LogFetchFailed(ILogger logger, string host, Exception failure);

    /// <remarks>The host, not the address: a path or query can carry a token.</remarks>
    [LoggerMessage(
        EventId = 1202,
        Level = LogLevel.Debug,
        Message = "{Host} answered {StatusCode} for an import")]
    private static partial void LogAnswered(ILogger logger, string host, int statusCode);
}
