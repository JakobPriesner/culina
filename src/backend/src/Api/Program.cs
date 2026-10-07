using Api;
using Api.Endpoints.Health;
using Api.Endpoints.Setup;
using Api.Endpoints.WellKnown;
using Api.Extensions;
using Api.Infrastructure;
using Application;
using Application.Abstractions.Settings;
using Infrastructure;
using Infrastructure.Settings;

// Bootstrap settings are read once and immutable, so a saved change applies by rebuilding the host
// in this loop. See IHostRestart.
while (true)
{
    var builder = WebApplication.CreateBuilder(args);

    // Health probe: makes one HTTP request and exits, before anything else is configured.
    if (HealthCheckProbe.Requested(args))
    {
        return await HealthCheckProbe.RunAsync(builder.Configuration).ConfigureAwait(false);
    }

    // Above appsettings.json, below the environment. See IServerConfiguration.
    builder.Configuration.AddServerConfigurationFile();

    // The export starts the host but must not take the app's port; port 0 is any free one.
    if (OpenApiExport.Requested(args))
    {
        builder.WebHost.UseUrls("http://127.0.0.1:0");
    }

    // No database configured yet: serve only the setup screen.
    var app = builder.Configuration.IsDatabaseConfigured() ? BuildCulina(builder) : BuildSetup(builder);

    // After the endpoints are mapped, since the document is built by enumerating them.
    if (OpenApiExport.Requested(args))
    {
        await OpenApiExport.WriteAsync(app, args).ConfigureAwait(false);

        return 0;
    }

    var restart = app.Services.GetRequiredService<HostRestart>();

    await app.RunAsync().ConfigureAwait(false);

    if (!restart.Requested)
    {
        return 0;
    }
}

static WebApplication BuildCulina(WebApplicationBuilder builder)
{
    builder.AddObservability();

    builder.Services
        .AddApplication()
        .AddInfrastructure(builder.Configuration, builder.Environment)
        .AddPresentation()
        .AddEndpoints();

    ValidateScopesInDevelopment(builder);

    var app = builder.Build();

    // THE ORDER OF THIS PIPELINE IS THE CONTRACT: moving a line is a security change and needs a test.
    // IntegrationTests/Pipeline asserts the ordering-dependent properties.

    app.UseCulinaForwardedHeaders();  //  1. Real client IP and scheme, before anything reads them.
    app.UseRequestContext();          //  2. Correlation id, so every later line carries it.
    app.UseSecurityHeaders();         //  3. Set before any handler can begin writing a body.
    app.UseHttpLogging();             //  4. One combined line per request.
    app.UseExceptionHandler();        //  5. Outside everything below: any defect becomes a problem document.
    app.UseProblemStatusPages();      //  6. Framework-generated statuses get a problem body too.
    app.UseSinglePageApp();           //  7. Static assets are cheap and never reach authentication.

    // Explicit, so the checks below can read endpoint metadata.
    app.UseRouting();

    app.UseQueryParameterGuard();     //  8. Reject unknown or repeated input before binding.
    app.UseSameOriginGuard();         //  9. Unsafe cookie-authenticated requests must come from us.
    app.UseRateLimiter();             // 10. Before authentication: brute force costs nothing to reject.

    app.UseAuthentication();          // 11. Cookie to principal.

    app.UseSessionContext();          // 12. User id on the logging scope and the span.
    app.UseCsrfGuard();               // 13. Needs the session to compare the token against.
    app.UseAuthorization();           // 14. Policies, after identity is established.
    app.UsePersonalRateLimits();      // 15. Costly limits per person, now that it is known who asks.

    app.MapHealthEndpoints();
    app.MapSecurityTxt();
    app.MapEndpoints();
    app.MapSinglePageAppFallback();

    LogStarting(app);

    return app;
}

static void LogStarting(WebApplication app)
{
    var database = app.Services.GetRequiredService<DatabaseSettings>();
    var storage = app.Services.GetRequiredService<StorageSettings>();
    var telemetry = app.Services.GetRequiredService<TelemetrySettings>();
    var version = ObservabilityExtensions.Version();

    app.Logger.Starting(
        version,
        app.Environment.EnvironmentName,
        database.Host,
        database.Port,
        database.Name,
        storage.ImagePath,
        telemetry.Endpoint?.Host ?? "nowhere");

    if (!app.Services.GetRequiredService<CookieSettings>().Secure)
    {
        app.Logger.InsecureCookies(app.Environment.EnvironmentName);
    }
}

// The host for an instance with no database: the same shell, headers and error format, but only setup
// endpoints, no forwarded headers (no proxy named yet), no sessions or CSRF, and only the fixed rate limit.
static WebApplication BuildSetup(WebApplicationBuilder builder)
{
    builder.AddObservability();

    builder.Services
        .AddSetupHandlers()
        .AddSetupInfrastructure()
        .AddSetupPresentation()
        .AddSetupEndpoints();

    ValidateScopesInDevelopment(builder);

    var app = builder.Build();

    app.UseRequestContext();
    app.UseSecurityHeaders();
    app.UseHttpLogging();
    app.UseExceptionHandler();
    app.UseProblemStatusPages();
    app.UseSinglePageApp();
    app.UseRouting();
    app.UseRateLimiter();
    app.UseAuthorization();

    app.MapSetupHealthEndpoints();
    app.MapEndpoints();
    app.MapSetupRequired();
    app.MapSinglePageAppFallback();

    app.Logger.WaitingForSetup();

    return app;
}

// Fails at startup instead of intermittently in production when a singleton captures a scoped service.
static void ValidateScopesInDevelopment(WebApplicationBuilder builder) =>
    builder.Host.UseDefaultServiceProvider(options =>
    {
        options.ValidateScopes = builder.Environment.IsDevelopment();
        options.ValidateOnBuild = builder.Environment.IsDevelopment();
    });
