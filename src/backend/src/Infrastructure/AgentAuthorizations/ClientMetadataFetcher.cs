using System.Text.Json;
using Application.Abstractions;
using Domain.AgentAuthorizations;
using Domain.Shared;
using Infrastructure.Import;
using Microsoft.Extensions.Logging;

namespace Infrastructure.AgentAuthorizations;

/// <summary>Reads an app's Client ID Metadata Document from the https address it uses as its <c>client_id</c>.</summary>
/// <remarks>
/// Anybody can make Culina fetch an address by opening its authorization endpoint, so the fetch is held to what the
/// draft's security considerations ask: public addresses only, connecting to the checked address (DNS rebinding), no
/// redirects (an unchecked second request, and a document that moved is not at its client_id), a short deadline and
/// a small body. Failures are vague on purpose, or the endpoint becomes a port scanner.
/// </remarks>
internal sealed partial class ClientMetadataFetcher : IClientMetadataDocuments, IDisposable
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    // The draft expects a few hundred bytes and lets a server refuse past 5 kB; room for a long list of addresses.
    private const int MaxBytes = 16 * 1024;

    private static readonly Error Unreadable = AgentAuthorizationErrors.InvalidClient(
        "its metadata document could not be read from its client_id address");

    private readonly SocketsHttpHandler handler;
    private readonly HttpClient client;
    private readonly ILogger<ClientMetadataFetcher> logger;

    public ClientMetadataFetcher(ILogger<ClientMetadataFetcher> logger)
    {
        this.logger = logger;

        handler = CheckedConnections.PublicHandler();
        client = new HttpClient(handler, disposeHandler: false) { Timeout = Deadline };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("Culina/1.0 (self-hosted OAuth authorization server)");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    public async Task<Result<ClientMetadataDocument>> FetchAsync(Uri address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.Scheme != Uri.UriSchemeHttps)
        {
            return Unreadable;
        }

        try
        {
            using var response = await client
                .GetAsync(address, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxBytes)
            {
                LogRefused(logger, address.Host, (int)response.StatusCode);

                return Unreadable;
            }

            var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            await using (body.ConfigureAwait(false))
            {
                using var buffer = new MemoryStream();

                return await BoundedRead.CopyAsync(body, buffer, MaxBytes, cancellationToken).ConfigureAwait(false)
                    ? Parse(buffer.ToArray())
                    : Unreadable;
            }
        }
        catch (Exception failure) when (failure is HttpRequestException or OperationCanceledException
                                            or InvalidOperationException or IOException)
        {
            // Logged with the host, never returned with it.
            LogFetchFailed(logger, address.Host, failure);

            return Unreadable;
        }
    }

    private static Result<ClientMetadataDocument> Parse(byte[] body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return Unreadable;
            }

            return new ClientMetadataDocument(
                Text(root, "client_id"),
                Text(root, "client_name"),
                root.TryGetProperty("redirect_uris", out var uris) && uris.ValueKind == JsonValueKind.Array
                    ? [.. uris.EnumerateArray().Where(uri => uri.ValueKind == JsonValueKind.String).Select(uri => uri.GetString()!)]
                    : []);
        }
        catch (JsonException)
        {
            return Unreadable;
        }
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public void Dispose()
    {
        client.Dispose();
        handler.Dispose();
    }

    [LoggerMessage(
        EventId = 1230,
        Level = LogLevel.Information,
        Message = "Could not fetch the client metadata document at {Host}")]
    private static partial void LogFetchFailed(ILogger logger, string host, Exception failure);

    [LoggerMessage(
        EventId = 1231,
        Level = LogLevel.Information,
        Message = "{Host} answered {StatusCode} for a client metadata document, or sent one too large")]
    private static partial void LogRefused(ILogger logger, string host, int statusCode);
}
