using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Application.Abstractions.Settings;
using Domain.Import;
using Domain.Shared;

namespace Infrastructure.Import;

/// <summary>
/// The one way this app talks to somebody else's recipe server: shared limits, deadline and read cap.
/// </summary>
/// <remarks>
/// Redirects are never followed (nothing legitimate redirects an API call, and one would carry the
/// <c>Authorization</c> header elsewhere); the token goes on each request because one client serves every household.
/// </remarks>
internal sealed class SourceHttp : IDisposable
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(20);

    private const int MaxBytes = 4 * 1024 * 1024;

    // Shorter than Deadline: a slow picture should give up long before a slow recipe, which is what the user waits for.
    private static readonly TimeSpan PictureDeadline = TimeSpan.FromSeconds(8);

    private readonly SocketsHttpHandler handler;
    private readonly HttpClient client;
    private readonly int maxPictureBytes;
    private readonly TimeSpan deadline;

    // Servers (host:port) ever dialled at a non-public address, remembered at connect time so a name that
    // resolves publicly once and privately later cannot slip through.
    private readonly ConcurrentDictionary<string, bool> privateServers =
        new(StringComparer.OrdinalIgnoreCase);

    public SourceHttp(ImportSettings settings, StorageSettings storage)
        : this(storage, Reach(settings))
    {
    }

    /// <summary>Separate from the public constructor so tests can reach loopback servers and use short deadlines.</summary>
    internal SourceHttp(StorageSettings storage, Func<IPAddress, bool> admits, TimeSpan? deadline = null)
    {
        ArgumentNullException.ThrowIfNull(storage);

        this.deadline = deadline ?? Deadline;

        // Same ceiling as an upload: the same code stores both.
        maxPictureBytes = storage.MaxImageBytes;

        // Redirects are never followed: a followed one would hand the token to wherever it pointed.
        handler = CheckedConnections.Handler(admits, dialling: Remember);

        client = new HttpClient(handler, disposeHandler: false) { Timeout = this.deadline };

        // Said plainly: this is one person moving their own recipes, not a crawler.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Culina/1.0 (self-hosted recipe import)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    // A refused token is the only failure reported specifically; everything else is "could not fetch" so
    // this cannot be used to probe which addresses answer. See VagueAsync.
    internal Task<Result<TBody>> GetAsync<TBody>(
        Uri url,
        AuthenticationHeaderValue authorization,
        CancellationToken cancellationToken)
        where TBody : notnull =>
        VagueAsync(url, deadline, token => SendAsync<TBody>(url, authorization, token), cancellationToken);

    // The one call that carries a password, kept apart so there is one answer to "where does it go".
    // Nothing posted is ever logged.
    internal Task<Result<TBody>> PostFormAsync<TBody>(
        Uri url,
        IReadOnlyDictionary<string, string> form,
        CancellationToken cancellationToken)
        where TBody : notnull =>
        VagueAsync(url, deadline, token => PostAsync<TBody>(url, form, token), cancellationToken);

    // Bytes, not a decoded image: IImageStore decodes and re-encodes. The content type is checked only to
    // avoid downloading large non-images; the read is capped while reading, since Content-Length is a claim.
    internal Task<Result<Stream>> GetPictureAsync(
        Uri url,
        AuthenticationHeaderValue authorization,
        CancellationToken cancellationToken) =>
        VagueAsync(url, PictureDeadline, token => ReadPictureAsync(url, authorization, token), cancellationToken);

    // The deadline covers the body too, since HttpClient.Timeout stops at the headers and a trickling
    // server would hold the call forever. Failures never name the host or reason (port-scanner risk), and
    // for a private-network server even "refused" and "too large" collapse to "could not fetch", so a
    // connection cannot map the internal network.
    private async Task<Result<T>> VagueAsync<T>(
        Uri url,
        TimeSpan limit,
        Func<CancellationToken, Task<Result<T>>> call,
        CancellationToken cancellationToken)
        where T : notnull
    {
        using var within = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        within.CancelAfter(limit);

        Result<T> answer;

        try
        {
            answer = await call(within.Token).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is HttpRequestException or OperationCanceledException
                                            or InvalidOperationException or IOException)
        {
            return ImportErrors.CouldNotFetch;
        }

        return privateServers.ContainsKey(ServerKey(url.IdnHost, url.Port))
            ? answer.Match(Result<T>.Success, _ => Result<T>.Failure(ImportErrors.CouldNotFetch))
            : answer;
    }

    private static Func<IPAddress, bool> Reach(ImportSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return settings.AllowPrivateSourceAddresses
            ? address => PublicAddress.IsPublic(address) || PublicAddress.IsPrivateNetwork(address)
            : PublicAddress.IsPublic;
    }

    private void Remember(DnsEndPoint server, IPAddress address)
    {
        if (!PublicAddress.IsPublic(address))
        {
            privateServers.TryAdd(ServerKey(server.Host, server.Port), true);
        }
    }

    // Without brackets: an IPv6 literal may arrive with or without them.
    private static string ServerKey(string host, int port) =>
        $"{host.Trim('[', ']')}:{port.ToString(CultureInfo.InvariantCulture)}";

    private async Task<Result<Stream>> ReadPictureAsync(
        Uri url,
        AuthenticationHeaderValue authorization,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.Authorization = authorization;
        request.Headers.Accept.Clear();
        request.Headers.Accept.ParseAdd("image/*");

        using var response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return ImportErrors.CouldNotFetch;
        }

        if (response.Content.Headers.ContentType?.MediaType is not { } mediaType
            || !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return ImportErrors.NotAPicture;
        }

        if (response.Content.Headers.ContentLength > maxPictureBytes)
        {
            return ImportErrors.TooLarge;
        }

        var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        await using (body.ConfigureAwait(false))
        {
            var buffer = new MemoryStream();

            try
            {
                using var capped = new CappedStream(body, maxPictureBytes);

                await capped.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

                if (capped.Overflowed)
                {
                    await buffer.DisposeAsync().ConfigureAwait(false);

                    return ImportErrors.TooLarge;
                }
            }
            catch
            {
                await buffer.DisposeAsync().ConfigureAwait(false);

                throw;
            }

            buffer.Position = 0;

            return buffer;
        }
    }

    private async Task<Result<TBody>> SendAsync<TBody>(
        Uri url,
        AuthenticationHeaderValue authorization,
        CancellationToken cancellationToken)
        where TBody : notnull
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.Authorization = authorization;

        using var response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        return await ReadAsync<TBody>(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<TBody>> PostAsync<TBody>(
        Uri url,
        IReadOnlyDictionary<string, string> form,
        CancellationToken cancellationToken)
        where TBody : notnull
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(form)
        };

        using var response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        return await ReadAsync<TBody>(response, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<TBody>> ReadAsync<TBody>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
        where TBody : notnull
    {
        // 400 too: an obtain-token endpoint answers a wrong password with a validation failure.
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            or HttpStatusCode.BadRequest)
        {
            return ImportErrors.SourceRefused;
        }

        if (!response.IsSuccessStatusCode)
        {
            return ImportErrors.CouldNotFetch;
        }

        if (response.Content.Headers.ContentLength > MaxBytes)
        {
            return ImportErrors.TooLarge;
        }

        var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        await using (body.ConfigureAwait(false))
        {
            // Capped while reading: Content-Length is a claim.
            using var capped = new CappedStream(body, MaxBytes);

            try
            {
                var parsed = await JsonSerializer
                    .DeserializeAsync<TBody>(capped, Json, cancellationToken)
                    .ConfigureAwait(false);

                // Asked even on success: a truncated document can parse when it ends on a boundary.
                if (capped.Overflowed)
                {
                    return ImportErrors.TooLarge;
                }

                return parsed is null ? ImportErrors.SourceNotUnderstood : parsed;
            }
            catch (JsonException)
            {
                return capped.Overflowed
                    ? ImportErrors.TooLarge
                    : ImportErrors.SourceNotUnderstood;
            }
        }
    }

    /// <summary>The format every source answers in.</summary>
    internal static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        // Other apps grow fields; a new one must not be a failed import.
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public void Dispose()
    {
        client.Dispose();
        handler.Dispose();
    }

    // Ends rather than throwing and reports Overflowed, so callers need no second path.
    private sealed class CappedStream(Stream inner, int limit) : Stream
    {
        private long read;

        /// <summary>Whether the body had more in it than was allowed.</summary>
        internal bool Overflowed { get; private set; }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => read;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Count(inner.Read(buffer, offset, count));

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            Count(await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false));

        public override void Flush() => inner.Flush();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        private int Count(int taken)
        {
            read += taken;

            if (read <= limit)
            {
                return taken;
            }

            Overflowed = true;

            // Ends here: a document cut in half is not a smaller document; callers check Overflowed.
            return 0;
        }
    }
}
