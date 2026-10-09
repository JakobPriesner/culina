using System.Net.Http.Json;
using System.Text.Json;

namespace IntegrationTests.Fixtures;

/// <summary>Drives the API the way a browser does.</summary>
/// <remarks>
/// The cookie jar and the CSRF header are not bypassed for convenience: that path is what the tests
/// prove, and a helper that skipped the token would make every security test pass for the wrong
/// reason. Responses come back as <see cref="ApiResponse"/>, not exceptions, so a test asserts on
/// status and problem <c>code</c> as the frontend branches.
/// </remarks>
public sealed class ApiClient(HttpClient http) : IDisposable
{
    private const string CsrfHeader = "X-Culina-CSRF";
    private const string CsrfCookie = "culina.csrf";

    /// <summary>The token echoed on unsafe requests, once a session exists.</summary>
    public string? CsrfToken { get; private set; }

    public Task<ApiResponse> GetAsync(string path, CancellationToken cancellationToken) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Get, path), cancellationToken);

    public Task<ApiResponse> PostAsync<TBody>(string path, TBody body, CancellationToken cancellationToken) =>
        SendAsync(
            new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) },
            cancellationToken);

    public Task<ApiResponse> PutAsync<TBody>(string path, TBody body, CancellationToken cancellationToken) =>
        SendAsync(
            new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) },
            cancellationToken);

    public Task<ApiResponse> PatchAsync<TBody>(string path, TBody body, CancellationToken cancellationToken) =>
        SendAsync(
            new HttpRequestMessage(HttpMethod.Patch, path) { Content = JsonContent.Create(body) },
            cancellationToken);

    public Task<ApiResponse> DeleteAsync(string path, CancellationToken cancellationToken) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Delete, path), cancellationToken);

    /// <summary>
    /// Deletes with a precondition (<c>ifMatch</c>: the ETag the caller holds, quotes included),
    /// for a resource whose delete requires one.
    /// </summary>
    public Task<ApiResponse> DeleteAsync(string path, string ifMatch, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, path);
        request.Headers.TryAddWithoutValidation("If-Match", ifMatch);

        return SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Reads what is at <paramref name="path"/> and deletes that version of it: somebody deleting
    /// the thing they are looking at.
    /// </summary>
    public async Task<ApiResponse> DeleteCurrentAsync(string path, CancellationToken cancellationToken)
    {
        var read = await GetAsync(path, cancellationToken);
        var etag = read.ETag ?? throw new InvalidOperationException($"{path} answered {read.StatusCode} without an ETag.");

        return await DeleteAsync(path, etag, cancellationToken);
    }

    /// <summary>
    /// Sends a request built by the caller, for header-level tests, taking ownership of the
    /// single-use message; <c>attachCsrf</c> false omits the CSRF header, the shape of a cross-site
    /// request riding this browser's cookie jar.
    /// </summary>
    public async Task<ApiResponse> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken,
        bool attachCsrf = true)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var owned = request;

        if (attachCsrf && CsrfToken is { } token && !IsSafe(request.Method))
        {
            request.Headers.TryAddWithoutValidation(CsrfHeader, token);
        }

        // Every unsafe request states where it came from, exactly as a browser
        // would; the same-origin guard rejects one that does not.
        request.Headers.Referrer ??= new Uri(http.BaseAddress!, "/");

        using var response = await http.SendAsync(request, cancellationToken);

        RememberCsrfToken(response);

        // Read as bytes, then decoded: an image response is not text, and a test looking inside
        // needs what was sent.
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        return new ApiResponse(
            response.StatusCode,
            response.Headers,
            response.Content.Headers,
            System.Text.Encoding.UTF8.GetString(bytes),
            bytes);
    }

    /// <summary>
    /// Opens a stream and returns once the headers arrive, so a test reads events as they are sent;
    /// the caller disposes the response.
    /// </summary>
    public async Task<HttpResponseMessage> OpenStreamAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.ParseAdd("text/event-stream");

        return await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static bool IsSafe(HttpMethod method) =>
        method == HttpMethod.Get || method == HttpMethod.Head || method == HttpMethod.Options;

    private void RememberCsrfToken(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return;
        }

        foreach (var cookie in cookies)
        {
            if (!cookie.StartsWith($"{CsrfCookie}=", StringComparison.Ordinal))
            {
                continue;
            }

            var value = cookie[(CsrfCookie.Length + 1)..].Split(';', 2)[0];

            CsrfToken = value.Length == 0 ? null : value;
        }
    }

    public void Dispose() => http.Dispose();
}

/// <summary>One response, in the shape a test wants to assert on.</summary>
/// <param name="StatusCode">What the server answered.</param>
/// <param name="Headers">Response headers.</param>
/// <param name="ContentHeaders">Content headers, including the content type.</param>
/// <param name="Body">The raw body, so a test can read either JSON or nothing.</param>
public sealed record ApiResponse(
    System.Net.HttpStatusCode StatusCode,
    System.Net.Http.Headers.HttpResponseHeaders Headers,
    System.Net.Http.Headers.HttpContentHeaders ContentHeaders,
    string Body,
    ReadOnlyMemory<byte> Bytes)
{
    /// <summary>The problem document's machine-readable code, if this is one.</summary>
    public string? ProblemCode => Json?.TryGetProperty("code", out var code) == true
        ? code.GetString()
        : null;

    /// <summary>The body parsed as JSON, or null when there is no body.</summary>
    public JsonElement? Json => string.IsNullOrWhiteSpace(Body)
        ? null
        : JsonSerializer.Deserialize<JsonElement>(Body);

    /// <summary>The strong entity tag, when the endpoint issued one.</summary>
    public string? ETag => Headers.ETag?.ToString();

    /// <summary>Reads the body as a typed response.</summary>
    /// <typeparam name="TBody">The expected shape.</typeparam>
    public TBody Read<TBody>() =>
        JsonSerializer.Deserialize<TBody>(Body, JsonOptions)
        ?? throw new InvalidOperationException($"Response body was not a {typeof(TBody).Name}: {Body}");

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);
}
