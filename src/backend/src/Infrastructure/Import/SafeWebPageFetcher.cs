using System.Net;
using System.Net.Sockets;
using Application.Abstractions;
using Domain.Import;
using Domain.Shared;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Import;

/// <summary>
/// Fetches a public web page, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// Every limit here exists because the server does the fetching. Without them
/// anyone with an account can aim this at the cloud metadata service, at the
/// database on the next container, or at an address whose <em>timing</em> tells
/// them what is listening on the private network.
/// </para>
/// <para>
/// Five controls, and all five are load-bearing:
/// </para>
/// <list type="number">
/// <item>Only <c>http</c> and <c>https</c>. <c>file:</c> reads the disk,
/// <c>gopher:</c> writes arbitrary bytes to an arbitrary port.</item>
/// <item>Every connection goes to an address that has been checked, because the
/// connection is made to the address this resolved — not to the name. A name
/// that resolves publicly when it is validated and to 127.0.0.1 when it is
/// dialled is the whole of DNS rebinding, and checking the name closes nothing.
/// </item>
/// <item>Redirects are followed by hand, a few at most, each one validated
/// again. Automatic redirects would take the second hop without any of this.
/// </item>
/// <item>A deadline on the whole exchange, so a server that answers one byte a
/// minute cannot hold a connection open.</item>
/// <item>A cap on what is read, enforced while reading rather than after, so a
/// response with no end cannot be the end of the process.</item>
/// </list>
/// <para>
/// What it reports is deliberately vague. "That address cannot be fetched" for
/// every refusal: telling a caller which address was blocked and which merely
/// timed out turns this into a port scanner with a friendly error message.
/// </para>
/// </remarks>
internal sealed partial class SafeWebPageFetcher : IWebPageFetcher, IDisposable
{
    /// <summary>How long the whole exchange may take.</summary>
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    /// <summary>How much of a page is read. A recipe page is a fraction of it.</summary>
    private const int MaxBytes = 2 * 1024 * 1024;

    /// <summary>
    /// How many redirects are followed.
    /// </summary>
    /// <remarks>
    /// Enough for the ordinary http → https → www chain, and far short of a
    /// loop. Each one is validated again from scratch.
    /// </remarks>
    private const int MaxHops = 5;

    private readonly SocketsHttpHandler handler;
    private readonly HttpClient client;
    private readonly ILogger<SafeWebPageFetcher> logger;

    public SafeWebPageFetcher(ILogger<SafeWebPageFetcher> logger)
    {
        this.logger = logger;

        handler = new SocketsHttpHandler
        {
            // Followed by hand instead, so every hop is validated.
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            ConnectCallback = ConnectToACheckedAddressAsync
        };

        client = new HttpClient(handler, disposeHandler: false) { Timeout = Deadline };

        // Said plainly. A fetcher that pretended to be a browser would be
        // making it harder for a site to say no, and this is an import for one
        // person's own use rather than a crawler.
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
            // Logged with the host, never returned with it. The operator can
            // see what happened; the caller learns only that it did not work.
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

            if (!IsRedirect(response.StatusCode))
            {
                return await ReadAsync(current, response, cancellationToken).ConfigureAwait(false);
            }

            if (response.Headers.Location is not { } location)
            {
                return ImportErrors.CouldNotFetch;
            }

            // Resolved against the page it came from, because a Location header
            // is allowed to be relative — and a relative one that is not
            // resolved would be fetched as a path on this server.
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

        if (mediaType is not ("text/html" or "application/xhtml+xml" or "text/plain"))
        {
            return ImportErrors.NotAWebPage;
        }

        // Checked before reading when the server says, and again while reading
        // when it does not: a Content-Length is a claim, not a limit.
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

    /// <summary>
    /// Resolves the host, refuses everything that is not the open internet, and
    /// connects to the address it checked.
    /// </summary>
    /// <remarks>
    /// Connecting to the <em>checked address</em> rather than to the host name
    /// is the entire point. Validating a name and then handing the name to the
    /// socket leaves a window in which the name can resolve to something else,
    /// and that window is DNS rebinding.
    /// </remarks>
    private static async ValueTask<Stream> ConnectToACheckedAddressAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;

        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);

        var allowed = Array.Find(addresses, PublicAddress.IsPublic)
            ?? throw new InvalidOperationException("The address is not on the public internet.");

#pragma warning disable CA2000 // The stream returned below owns the socket.
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
#pragma warning restore CA2000

        try
        {
            await socket
                .ConnectAsync(new IPEndPoint(allowed, context.DnsEndPoint.Port), cancellationToken)
                .ConfigureAwait(false);

            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();

            throw;
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
        EventId = 2101,
        Level = LogLevel.Information,
        Message = "Could not fetch {Host} for an import")]
    private static partial void LogFetchFailed(ILogger logger, string host, Exception failure);
}
