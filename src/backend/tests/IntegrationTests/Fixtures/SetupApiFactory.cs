using Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TestSupport;

namespace IntegrationTests.Fixtures;

/// <summary>
/// The app as a fresh container starts it: no database configured anywhere.
/// </summary>
/// <remarks>
/// So <c>Program</c> builds the setup host rather than the real one, which is
/// the thing under test. Storage points at a directory of its own, exactly as
/// <see cref="CulinaApiFactory"/> does, so the settings file a test saves is
/// one it can read and nobody else sees.
/// </remarks>
public sealed class SetupApiFactory : WebApplicationFactory<Program>
{
    private readonly string dataRoot =
        Path.Combine(Path.GetTempPath(), $"culina-setup-{Guid.CreateVersion7():n}");

    /// <summary>Every restart a saved setting asked for, none of them performed.</summary>
    public RecordingRestart Restarts { get; } = new();

    /// <summary>The settings file the database step writes.</summary>
    public string ServerSettingsFile => Path.Combine(dataRoot, "config", "culina.json");

    public ApiClient NewApiClient() => new(CreateDefaultClient(new CookieHandler()));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Production");
        builder.UseSetting("Storage:ImagePath", Path.Combine(dataRoot, "images"));
        builder.UseSetting("Storage:DataProtectionKeyPath", Path.Combine(dataRoot, "keys"));
        builder.UseSetting("Storage:ConfigPath", Path.Combine(dataRoot, "config"));

        builder.ConfigureTestServices(services => services.AddSingleton<IHostRestart>(Restarts));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(dataRoot))
        {
            Directory.Delete(dataRoot, recursive: true);
        }
    }
}
