using System.Globalization;
using System.Net;
using Application.Abstractions;
using Domain.Suggestions;
using Infrastructure.Import;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TestSupport;

namespace IntegrationTests.Fixtures;
/// <summary>The real API host, pointed at the test container.</summary>
/// <remarks>
/// Starts exactly as in production (pipeline, middleware order, migrations), since that ordering is
/// what the tests prove. Settings go through <see cref="IWebHostBuilder.UseSetting"/>: Program
/// reads <c>builder.Configuration</c> before <c>ConfigureAppConfiguration</c> callbacks run.
/// </remarks>
/// <param name="postgres">The database the host uses.</param>
/// <param name="overrides">
/// Extra configuration for one test class. Rate limits default generous, so a shared client address
/// does not trip them; the limit itself is proven by a test that lowers it.
/// </param>
/// <param name="weights">
/// Ranking weights to score with, to hold some still; replaced in the container, as a weight is not
/// configuration.
/// </param>
/// <param name="replace">
/// Services swapped in after the app's own, for what no setting may change (a source client that
/// can reach loopback).
/// </param>
/// <param name="servesIntakes">
/// Whether this host's background worker takes recipe intakes off the shared database. Only the
/// one host a test submits intakes to may: every host has its own settings, so a second worker
/// would claim a job with an assistant that was never configured and fail it.
/// </param>
public sealed class CulinaApiFactory(
    PostgresFixture postgres,
    IReadOnlyDictionary<string, string>? overrides = null,
    RankingWeights? weights = null,
    Action<IServiceCollection>? replace = null,
    bool servesIntakes = false) : WebApplicationFactory<Program>
{
    private readonly string dataRoot =
        Path.Combine(Path.GetTempPath(), $"culina-test-{Guid.CreateVersion7():n}");

    /// <summary>Every restart a saved server setting asked for, none of them performed.</summary>
    public RecordingRestart Restarts { get; } = new();

    /// <summary>Every line the host logged, after the configured level filters.</summary>
    public RecordingLogs Logs { get; } = new();

    /// <summary>
    /// The settings file this host reads at startup and writes when a server setting is saved.
    /// </summary>
    public string ServerSettingsFile => Path.Combine(dataRoot, "config", "culina.json");

    private int pushRequests;
    /// <summary>Encrypted pushes captured locally; no test contacts a real push service.</summary>
    public int PushRequests => Volatile.Read(ref pushRequests);

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The DI-owned PushTransport disposes its HttpClient and handler.")]
    private PushTransport LocalPush() => new(new HttpClient(new PushHandler(() => Interlocked.Increment(ref pushRequests)), disposeHandler: true));

    private sealed class PushHandler(Action sent) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            sent();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
        }
    }

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
        // No TLS over the test client, so a __Host- cookie would be refused; production refuses to
        // start like that unless the deployment says so, and this one does.
        builder.UseSetting("Cookies:Secure", "false");
        builder.UseSetting("Cookies:AllowInsecureOutsideDevelopment", "true");

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
            services.AddSingleton<ILoggerProvider>(Logs);
            services.AddSingleton(_ => LocalPush());

            if (!servesIntakes)
            {
                foreach (var worker in services.Where(one => one.ImplementationType == typeof(RecipeIntakeWorker)).ToList())
                {
                    services.Remove(worker);
                }
            }

            if (weights is not null)
            {
                services.AddSingleton(weights);
            }

            replace?.Invoke(services);
        });
    }

    /// <summary>
    /// A client with its own cookie jar, so each test is an independent browser session.
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
/// Keeps cookies across requests as a browser does; the in-memory test server's HttpClient has
/// none, so a session would be dropped after the response that created it.
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
                // The __Host- prefix requires Secure, which the test server does not set, so the
                // container is told directly, skipping the prefix validation.
                cookies.SetCookies(uri, setCookie.Replace("; Secure", string.Empty, StringComparison.Ordinal));
            }
        }

        return response;
    }
}
