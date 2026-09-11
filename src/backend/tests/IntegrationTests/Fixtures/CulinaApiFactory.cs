using System.Globalization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;

namespace IntegrationTests.Fixtures;

/// <summary>
/// The real API host, pointed at the test container.
/// </summary>
/// <remarks>
/// <para>
/// The app starts exactly as it runs in production — same pipeline, same
/// middleware order, same migrations — because the ordering of that pipeline is
/// the thing these tests exist to prove.
/// </para>
/// <para>
/// Values are supplied through <see cref="IWebHostBuilder.UseSetting"/> rather
/// than <c>ConfigureAppConfiguration</c>. Under minimal hosting, Program reads
/// <c>builder.Configuration</c> while building the host, which is before
/// <c>ConfigureAppConfiguration</c> callbacks run; <c>UseSetting</c> lands in
/// host configuration and is visible in time.
/// </para>
/// </remarks>
/// <param name="postgres">The database the host should use.</param>
public sealed class CulinaApiFactory(PostgresFixture postgres) : WebApplicationFactory<Program>
{
    private readonly string dataRoot =
        Path.Combine(Path.GetTempPath(), $"culina-test-{Guid.CreateVersion7():n}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var settings = postgres.Settings;

        builder.UseEnvironment("Production");

        builder.UseSetting("Database:Host", settings.Host);
        builder.UseSetting("Database:Port", settings.Port.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("Database:Name", settings.Name);
        builder.UseSetting("Database:Username", settings.Username);
        builder.UseSetting("Database:Password", settings.Password);
        builder.UseSetting("Database:RequireSsl", "false");
        builder.UseSetting("Storage:ImagePath", Path.Combine(dataRoot, "images"));
        builder.UseSetting("Storage:DataProtectionKeyPath", Path.Combine(dataRoot, "keys"));
        // There is no TLS over the test client, so a __Host- cookie would be
        // refused outright.
        builder.UseSetting("Cookies:Secure", "false");
    }

    /// <summary>
    /// A client with its own cookie jar, so each test is an independent
    /// browser session.
    /// </summary>
    public ApiClient NewApiClient() =>
        new(CreateDefaultClient(new CookieHandler()));

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(dataRoot))
        {
            Directory.Delete(dataRoot, recursive: true);
        }
    }
}

/// <summary>
/// Keeps cookies across requests, the way a browser does. The in-memory test
/// server hands out an HttpClient with no cookie handling at all, so without
/// this a session would be dropped after the response that created it.
/// </summary>
internal sealed class CookieHandler : DelegatingHandler
{
    private readonly CookieContainer cookies = new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var uri = request.RequestUri!;
        var header = cookies.GetCookieHeader(uri);

        if (header.Length > 0)
        {
            request.Headers.Remove("Cookie");
            request.Headers.TryAddWithoutValidation("Cookie", header);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (var setCookie in setCookies)
            {
                // The __Host- prefix requires Secure, which the test server does
                // not set, so the container is told about the cookie directly
                // rather than through SetCookies' prefix validation.
                cookies.SetCookies(uri, setCookie.Replace("; Secure", string.Empty, StringComparison.Ordinal));
            }
        }

        return response;
    }
}
