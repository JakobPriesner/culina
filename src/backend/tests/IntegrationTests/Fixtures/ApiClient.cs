using System.Net.Http.Json;
using System.Text.Json;

namespace IntegrationTests.Fixtures;

/// <summary>
/// Drives the API the way a browser does.
/// </summary>
/// <remarks>
/// <para>
/// The cookie jar and the CSRF header are not bypassed for convenience: that
/// path is precisely what these tests exist to prove. A helper that quietly
/// skipped the CSRF token would make every security test pass for the wrong
/// reason.
/// </para>
/// <para>
/// Responses come back as <see cref="ApiResponse"/> rather than as a thrown
/// exception, so a test asserts on a status and a problem <c>code</c> the same
/// way the frontend branches on them.
/// </para>
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
    /// Sends a request built by the caller, for header-level tests. Takes
    /// ownership of the message, which is single-use.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <param name="attachCsrf">
    /// False to deliberately omit the CSRF header, which is the shape of a
    /// cross-site request riding this browser's cookie jar.
    /// </param>
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

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        return new ApiResponse(response.StatusCode, response.Headers, response.Content.Headers, body);
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
    string Body)
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
