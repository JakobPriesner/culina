using System.Net;
using System.Text;
using System.Text.Json;
using Domain.Assistance;
using Domain.Shared;

namespace Infrastructure.Assistance;

/// <summary>
/// The one way this app talks to a model provider.
/// </summary>
/// <remarks>
/// <para>
/// Built on the same reasoning as <see cref="Infrastructure.Import.SourceHttp"/>
/// and deliberately not shared with it: that one guards against an address a
/// user typed and so refuses private networks, while this one is pointed at an
/// address an administrator configured and pointing it at the machine next door
/// is the ordinary reason to configure it at all. Two clients with two threat
/// models beats one with a flag.
/// </para>
/// <para>
/// Redirects are not followed, for the same reason: a followed redirect would
/// carry the API key to wherever it pointed. The key goes on each request
/// rather than onto the client, because the settings it comes from can change
/// between two requests without a restart.
/// </para>
/// </remarks>
internal sealed class AssistantHttp : IDisposable
{
    /// <summary>
    /// How long one call may take.
    /// </summary>
    /// <remarks>
    /// Generous, because this is the slow thing: a model writing a whole recipe
    /// is tens of seconds on a bad day, and the frontend already waits sixty
    /// for it. Short enough that a provider which has stopped answering does
    /// not hold a request open until the proxy closes it.
    /// </remarks>
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(55);

    /// <summary>
    /// How much of an answer is read.
    /// </summary>
    /// <remarks>
    /// A recipe is a few kilobytes of JSON; a generated image is a megabyte or
    /// two of base64. Eight is far above both and far below what an unbounded
    /// read costs when the thing on the other end is not what it claimed.
    /// </remarks>
    private const int MaxBytes = 8 * 1024 * 1024;

    private readonly SocketsHttpHandler handler;
    private readonly HttpClient client;

    public AssistantHttp()
    {
        handler = new SocketsHttpHandler
        {
            // Nothing legitimate redirects an API call, and a followed redirect
            // would hand the key to whatever it pointed at.
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(10)
        };

        client = new HttpClient(handler, disposeHandler: false) { Timeout = Deadline };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Culina/1.0 (self-hosted recipe app)");
    }

    /// <summary>The shape every provider is spoken to and answers in.</summary>
    internal static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Posts JSON and reads JSON back, or says why it could not.
    /// </summary>
    /// <typeparam name="TBody">The shape expected back.</typeparam>
    /// <param name="url">What to post to.</param>
    /// <param name="payload">The request body.</param>
    /// <param name="authorize">Puts the credential on the request.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <remarks>
    /// Nothing about the body is logged — not on success, not on failure, and
    /// not the answer. The body carries whatever somebody typed into a recipe,
    /// and the headers carry the key.
    /// </remarks>
    internal async Task<Result<TBody>> PostAsync<TBody>(
        Uri url,
        object payload,
        Action<HttpRequestMessage> authorize,
        CancellationToken cancellationToken)
        where TBody : notnull
    {
        ArgumentNullException.ThrowIfNull(authorize);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload, Json),
                    Encoding.UTF8,
                    "application/json")
            };

            authorize(request);

            using var response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            return await ReadAsync<TBody>(response, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is HttpRequestException or OperationCanceledException
                                            or InvalidOperationException or IOException)
        {
            // The provider is unreachable, or took longer than the deadline.
            // One error for both, because neither is the caller's to fix.
            return AssistanceErrors.Unavailable;
        }
    }

    private static async Task<Result<TBody>> ReadAsync<TBody>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
        where TBody : notnull
    {
        if (!response.IsSuccessStatusCode)
        {
            return Refusal(response.StatusCode);
        }

        if (response.Content.Headers.ContentLength > MaxBytes)
        {
            return AssistanceErrors.UnusableAnswer;
        }

        var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        await using (body.ConfigureAwait(false))
        {
            try
            {
                var parsed = await JsonSerializer
                    .DeserializeAsync<TBody>(body, Json, cancellationToken)
                    .ConfigureAwait(false);

                return parsed is null ? AssistanceErrors.UnusableAnswer : parsed;
            }
            catch (JsonException)
            {
                return AssistanceErrors.UnusableAnswer;
            }
        }
    }

    /// <summary>
    /// What a status code from a provider means.
    /// </summary>
    /// <remarks>
    /// A wrong key is reported as unavailable rather than as itself, and
    /// deliberately: the person who sees this message is cooking, and the
    /// person who can fix it is the administrator, who has the log line. Told
    /// apart in the logs, not on screen.
    /// </remarks>
    private static Error Refusal(HttpStatusCode status) => status switch
    {
        HttpStatusCode.TooManyRequests => AssistanceErrors.Throttled,
        // Both providers answer a content-filter refusal with 400. So does a
        // malformed request, which is this app's defect — but the caller's
        // options are the same either way, and the log line tells them apart.
        HttpStatusCode.BadRequest => AssistanceErrors.Refused,
        _ => AssistanceErrors.Unavailable
    };

    /// <summary>
    /// Where the provider is: what was configured, or its usual address.
    /// </summary>
    /// <param name="configured">The override from settings, possibly empty.</param>
    /// <param name="fallback">The provider's own address.</param>
    /// <param name="path">The path to call, without a leading slash.</param>
    internal static Uri Address(string configured, string fallback, string path)
    {
        var origin = configured.Length > 0 ? configured : fallback;

        return new Uri(new Uri(origin.TrimEnd('/') + "/"), path);
    }

    public void Dispose()
    {
        client.Dispose();
        handler.Dispose();
    }
}
