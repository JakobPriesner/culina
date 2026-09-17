using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Application.Abstractions.Settings;
using Domain.Import;
using Domain.Shared;

namespace Infrastructure.Import;

/// <summary>
/// The one way this app talks to somebody else's recipe server.
/// </summary>
/// <remarks>
/// <para>
/// Shared by every library reader, so the limits are written once rather than
/// once per source: the same checked connections, the same deadline, the same
/// cap on how much of an answer is read, and the same rule about what a failure
/// is allowed to say.
/// </para>
/// <para>
/// Two things here are not details. Redirects are not followed at all — unlike
/// the pasted-link path, where a redirect is the ordinary shape of the web,
/// nothing legitimate redirects an API call, and a redirect that was followed
/// would carry an <c>Authorization</c> header to wherever it pointed. And the
/// token goes on each request rather than onto the client, because one client
/// serves every household's connections.
/// </para>
/// </remarks>
internal sealed class SourceHttp : IDisposable
{
    /// <summary>How long one call to the other app may take.</summary>
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(20);

    /// <summary>
    /// How much of an answer is read.
    /// </summary>
    /// <remarks>
    /// A page of fifty recipe summaries is a few hundred kilobytes and one
    /// recipe is a few. Four megabytes is far above both and far below what an
    /// unbounded read costs when the thing on the other end is not what it
    /// claimed to be.
    /// </remarks>
    private const int MaxBytes = 4 * 1024 * 1024;

    /// <summary>
    /// How long one picture may take.
    /// </summary>
    /// <remarks>
    /// Shorter than <see cref="Deadline"/>, and deliberately. A batch fetches
    /// up to twenty-five recipes and then up to twenty-five pictures, and a
    /// picture is the part nobody is waiting for: a recipe with no photo is
    /// still the recipe, so a slow image gives up long before a slow recipe
    /// would.
    /// </remarks>
    private static readonly TimeSpan PictureDeadline = TimeSpan.FromSeconds(8);

    private readonly SocketsHttpHandler handler;
    private readonly HttpClient client;
    private readonly int maxPictureBytes;

    public SourceHttp(ImportSettings settings, StorageSettings storage)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(storage);

        // The same ceiling an upload gets. A picture that arrives from another
        // app is stored by exactly the same code that stores one somebody
        // chose from their phone, so it may as well be bounded by the same
        // number rather than a second one that can drift away from it.
        maxPictureBytes = storage.MaxImageBytes;

        handler = new SocketsHttpHandler
        {
            // Nothing legitimate redirects an API call, and a followed redirect
            // would hand the token to whatever it pointed at.
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            ConnectCallback = CheckedConnections.To(settings.AllowPrivateSourceAddresses)
        };

        client = new HttpClient(handler, disposeHandler: false) { Timeout = Deadline };

        // Said plainly, as the page fetcher does. This is one person moving
        // their own recipes, not a crawler, and pretending to be a browser
        // would only be making it harder for a server to say no.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Culina/1.0 (self-hosted recipe import)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    /// <summary>
    /// Reads JSON from the other app, or says — vaguely — why it could not.
    /// </summary>
    /// <typeparam name="TBody">The shape expected back.</typeparam>
    /// <param name="url">What to ask for.</param>
    /// <param name="authorization">The <c>Authorization</c> header to send.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <remarks>
    /// A refused token is the one failure reported specifically, because it is
    /// the one thing the person can actually fix and the one thing they are
    /// most likely to have got wrong. Everything else is "could not fetch",
    /// deliberately: this endpoint must not become a way to ask which addresses
    /// answer and which merely time out.
    /// </remarks>
    internal async Task<Result<TBody>> GetAsync<TBody>(
        Uri url,
        AuthenticationHeaderValue authorization,
        CancellationToken cancellationToken)
        where TBody : notnull
    {
        try
        {
            return await SendAsync<TBody>(url, authorization, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is HttpRequestException or OperationCanceledException
                                            or InvalidOperationException or IOException)
        {
            // Never with the host or the reason attached. Telling a caller that
            // one address was refused and another merely timed out is how this
            // would become a port scanner with a friendly error message.
            return ImportErrors.CouldNotFetch;
        }
    }

    /// <summary>
    /// Posts a form and reads JSON back, without an <c>Authorization</c> header.
    /// </summary>
    /// <typeparam name="TBody">The shape expected back.</typeparam>
    /// <param name="url">What to post to.</param>
    /// <param name="form">The fields to send.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <remarks>
    /// <para>
    /// The one call in this class that carries somebody's password, and the
    /// reason it is written separately rather than folded into
    /// <see cref="GetAsync{TBody}"/>: there is exactly one of it, and a reader
    /// asking "where does the password go" should find one answer.
    /// </para>
    /// <para>
    /// A form rather than JSON, because that is what the endpoints this exists
    /// for accept. Nothing about what is posted is logged — not the fields, not
    /// their names, and not the body on a failure.
    /// </para>
    /// </remarks>
    internal async Task<Result<TBody>> PostFormAsync<TBody>(
        Uri url,
        IReadOnlyDictionary<string, string> form,
        CancellationToken cancellationToken)
        where TBody : notnull
    {
        try
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
        catch (Exception failure) when (failure is HttpRequestException or OperationCanceledException
                                            or InvalidOperationException or IOException)
        {
            return ImportErrors.CouldNotFetch;
        }
    }

    /// <summary>
    /// Reads a picture, and refuses anything that is not one.
    /// </summary>
    /// <param name="url">Where the picture is.</param>
    /// <param name="authorization">The <c>Authorization</c> header to send.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <remarks>
    /// <para>
    /// Bytes, not a decoded image: what comes back goes straight to
    /// <see cref="Application.Abstractions.IImageStore"/>, which decides what
    /// the file actually is by decoding it and stores its own re-encoding.
    /// Nothing here trusts the content type — it is checked only to avoid
    /// pulling ten megabytes of HTML down before the store rejects it.
    /// </para>
    /// <para>
    /// Capped while reading rather than after, for the same reason every other
    /// read here is: a <c>Content-Length</c> is a claim, and a response with no
    /// end must not be the end of the process.
    /// </para>
    /// </remarks>
    internal async Task<Result<Stream>> GetPictureAsync(
        Uri url,
        AuthenticationHeaderValue authorization,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        deadline.CancelAfter(PictureDeadline);

        try
        {
            return await ReadPictureAsync(url, authorization, deadline.Token).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is HttpRequestException or OperationCanceledException
                                            or InvalidOperationException or IOException)
        {
            return ImportErrors.CouldNotFetch;
        }
    }

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

    private static async Task<Result<TBody>> ReadAsync<TBody>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
        where TBody : notnull
    {
        // 400 as well as 401: an obtain-token endpoint answers a wrong password
        // with a validation failure, and reporting that as "could not fetch"
        // would send somebody to check an address that was right.
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
            // Capped while reading and not after: a Content-Length is a claim,
            // and an answer with no end must not be the end of the process.
            using var capped = new CappedStream(body, MaxBytes);

            try
            {
                var parsed = await JsonSerializer
                    .DeserializeAsync<TBody>(capped, Json, cancellationToken)
                    .ConfigureAwait(false);

                // Asked even on success: a truncated document can still parse
                // when it happens to end on a boundary, and what it parses to
                // is half an answer.
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

    /// <summary>
    /// A stream that stops at a limit and says that it did.
    /// </summary>
    /// <remarks>
    /// It ends rather than throwing, and reports <see cref="Overflowed"/>
    /// afterwards. An exception would have to be a type of its own to be caught
    /// precisely, a public one to satisfy the analyzers, and documented — all
    /// for a signal that never leaves this class. Ending is also what a reader
    /// already copes with, so neither caller needs a second path.
    /// </remarks>
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

            // Ends here. What has been read is not handed on: a document cut in
            // half is not a smaller document, and both callers ask about
            // Overflowed before they trust what they got.
            return 0;
        }
    }
}
