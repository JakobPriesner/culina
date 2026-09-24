using System.Globalization;
using System.Net;
using Application.Abstractions;
using Domain.Suggestions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TestSupport;

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
/// <param name="overrides">
/// Extra configuration for one test class. Rate limits in particular are
/// deliberately generous here: sharing one client address across a suite would
/// otherwise trip the production limits, and a test that fails because the
/// limiter works is a test that teaches people to remove the limiter. The limit
/// itself is proven by a test that lowers it on purpose.
/// </param>
/// <param name="weights">
/// The ranking weights this host should score with, when a test needs to hold
/// some of them still. <c>RankingWeights</c> is a record for exactly this —
/// a rule is proven by fixing nine terms and moving the tenth — and a weight
/// is not configuration, so it is replaced in the container rather than set.
/// </param>
public sealed class CulinaApiFactory(
    PostgresFixture postgres,
    IReadOnlyDictionary<string, string>? overrides = null,
    RankingWeights? weights = null) : WebApplicationFactory<Program>
{
    private readonly string dataRoot =
        Path.Combine(Path.GetTempPath(), $"culina-test-{Guid.CreateVersion7():n}");

    /// <summary>Every restart a saved server setting asked for, none of them performed.</summary>
    public RecordingRestart Restarts { get; } = new();

    /// <summary>The settings file this host reads at startup and writes when a server setting is saved.</summary>
    public string ServerSettingsFile => Path.Combine(dataRoot, "config", "culina.json");

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
        builder.UseSetting("Storage:ConfigPath", Path.Combine(dataRoot, "config"));
        // There is no TLS over the test client, so a __Host- cookie would be
        // refused outright.
        builder.UseSetting("Cookies:Secure", "false");

        builder.UseSetting("RateLimits:RegisterPerIpPerHour", "10000");
        builder.UseSetting("RateLimits:LoginPerIpPerMinute", "10000");
        builder.UseSetting("RateLimits:LoginPerAccountPerMinute", "10000");
        builder.UseSetting("RateLimits:InvitationPerIpPerHour", "10000");
        builder.UseSetting("RateLimits:RequestsPerSessionPerMinute", "100000");

        foreach (var (key, value) in overrides ?? new Dictionary<string, string>())
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            // Never the real restart: it would stop the host this factory
            // serves every later request from.
            services.AddSingleton<IHostRestart>(Restarts);

            if (weights is not null)
            {
                services.AddSingleton(weights);
            }
        });
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
