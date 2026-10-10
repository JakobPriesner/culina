using System.Reflection;
using Api.Infrastructure;
using Application.Abstractions.Settings;
using Application.Telemetry;
using Microsoft.Extensions.Logging.Console;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Api.Extensions;

/// <summary>All three telemetry signals, configured in one place.</summary>
/// <remarks>
/// A production incident must be answerable from telemetry alone: nobody can attach a debugger to a
/// self-hosted instance. Export is controlled by the standard <c>OTEL_*</c> names, read through the
/// host configuration so an endpoint saved in server settings works like a variable; with none set
/// the app still logs to stdout.
/// </remarks>
internal static class ObservabilityExtensions
{
    /// <summary>The standard name an operator sets to tell two instances apart at one collector.</summary>
    internal const string ServiceNameKey = "OTEL_SERVICE_NAME";

    /// <summary>The API's service name when <see cref="ServiceNameKey"/> is not set.</summary>
    internal const string ApiService = "culina-api";

    /// <summary>The web app's service name when <see cref="ServiceNameKey"/> is not set.</summary>
    internal const string WebAppService = "culina-web";

    internal static string ApiServiceName(IConfiguration configuration) =>
        string.IsNullOrWhiteSpace(configuration[ServiceNameKey]) ? ApiService : configuration[ServiceNameKey]!.Trim();

    /// <summary>
    /// The service what the web app reported is exported as: the API's name with <c>-web</c> in
    /// place of an <c>-api</c> ending, or after it, so <c>culina-api</c> pairs with <c>culina-web</c>.
    /// </summary>
    internal static string WebAppServiceName(IConfiguration configuration)
    {
        var api = ApiServiceName(configuration);

        return api.EndsWith("-api", StringComparison.Ordinal) ? $"{api[..^4]}-web" : $"{api}-web";
    }

    internal static IHostApplicationBuilder AddObservability(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureLogging();

        var telemetry = builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddCulinaService(ApiServiceName(builder.Configuration), Version(), builder.Environment)
                .AddMachine())
            .WithTracing(ConfigureTracing)
            .WithMetrics(ConfigureMetrics);

        if (Exports(builder.Configuration))
        {
            telemetry.UseOtlpExporter();
        }

        return builder;
    }

    /// <summary>
    /// Only when a collector is configured; otherwise the exporter retries against localhost
    /// forever and fills the log.
    /// </summary>
    private static bool Exports(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration[TelemetrySettings.EndpointKey]);

    private static ResourceBuilder AddCulinaService(
        this ResourceBuilder resource,
        string serviceName,
        string? serviceVersion,
        IHostEnvironment environment) =>
        resource
            .AddService(
                serviceName: serviceName,
                serviceVersion: serviceVersion,
                serviceInstanceId: Environment.MachineName)
            .AddAttributes([
                new KeyValuePair<string, object>("deployment.environment.name", environment.EnvironmentName)
            ]);

    /// <summary>
    /// The web app as a service, without a version: the server's would be wrong for a tab still on
    /// an older build. Each record names its own build in <c>culina.web.app_version</c>.
    /// </summary>
    internal static ResourceBuilder AddWebAppService(
        this ResourceBuilder resource,
        IConfiguration configuration,
        IHostEnvironment environment) =>
        resource.AddCulinaService(WebAppServiceName(configuration), serviceVersion: null, environment);

    /// <summary>
    /// The machine the server runs on, on every span, metric and line: host, container, operating
    /// system, runtime and process. Not the web app's, whose machine is the browser's.
    /// </summary>
    private static ResourceBuilder AddMachine(this ResourceBuilder resource) =>
        resource
            .AddHostDetector()
            .AddOperatingSystemDetector()
            .AddContainerDetector()
            .AddProcessDetector()
            .AddProcessRuntimeDetector();

    private static void ConfigureLogging(this IHostApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();

        // Every line carries trace and span ids, so a request's lines correlate, and so do those of
        // work no request carries (import, sweep, migration). The trace id is the request id a user
        // is shown.
        builder.Logging.Configure(options =>
            options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);

        // The host opens a scope naming every request's raw path, which would put a share token or
        // invitation code on every line. Silenced: the request line already covers start and
        // finish, redacted.
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.None);

        if (builder.Environment.IsDevelopment())
        {
            builder.Logging.AddSimpleConsole(console =>
            {
                console.SingleLine = true;
                console.IncludeScopes = true;
                console.TimestampFormat = "HH:mm:ss ";
            });
        }
        else
        {
            // JSON in production so a collector can parse fields.
            builder.Logging.AddJsonConsole(console =>
            {
                console.IncludeScopes = true;
                console.JsonWriterOptions = new System.Text.Json.JsonWriterOptions { Indented = false };
            });
        }

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        // What the web app reported is exported as the web app, not the API that passed it on; it
        // still reaches the console.
        builder.Logging.AddFilter<OpenTelemetryLoggerProvider>(CulinaTelemetry.WebAppCategory, LogLevel.None);
        builder.Services.AddSingleton<ILoggerProvider>(services =>
        {
            var configuration = services.GetRequiredService<IConfiguration>();

            return new WebAppLoggerProvider(
                configuration,
                resource => resource.AddWebAppService(configuration, builder.Environment),
                logging =>
                {
                    if (Exports(configuration))
                    {
                        logging.AddOtlpExporter();
                    }
                });
        });
    }

    private static void ConfigureTracing(TracerProviderBuilder tracing) =>
        tracing
            .AddSource(CulinaTelemetry.Name)
            .AddAspNetCoreInstrumentation(options =>
            {
                // Health checks are most of the traffic and none of the information.
                options.Filter = context => !context.Request.Path.StartsWithSegments("/health");

                // Without the share token or invitation code a path can carry: the collector's
                // readers must not be handed what it opens.
                options.EnrichWithHttpRequest = (activity, request) =>
                    activity.SetTag("url.path", SecretPaths.Redact(request.PathBase.Add(request.Path)));
            })
            .AddHttpClientInstrumentation()
            .AddNpgsql();

    private static void ConfigureMetrics(MeterProviderBuilder metrics) =>
        metrics
            .AddMeter(CulinaTelemetry.Name)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            // Process CPU, memory and threads as the OS counts them, beside the runtime's view of
            // its heap.
            .AddProcessInstrumentation()
            .AddMeter(MachineMetrics.MeterName)
            .AddInstrumentation(services => new MachineMetrics(services.GetService<StorageSettings>()))
            // Npgsql publishes pool and command metrics on its own meter, not through an
            // instrumentation package.
            .AddMeter("Npgsql");

    internal static string Version() =>
        typeof(ObservabilityExtensions).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";
}
