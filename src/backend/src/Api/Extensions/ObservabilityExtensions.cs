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

/// <summary>
/// All three telemetry signals, configured in one place.
/// </summary>
/// <remarks>
/// <para>
/// A production incident must be answerable from the telemetry alone: nobody
/// can attach a debugger to a self-hosted instance, and nobody may be asked to
/// reproduce it with more logging turned on.
/// </para>
/// <para>
/// Culina invents no configuration names of its own. The standard
/// <c>OTEL_*</c> names control export, and with
/// <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> unset the app still logs to stdout and
/// simply exports nothing. The SDK reads them through the host's
/// configuration, not the process environment, so an endpoint saved from the
/// server settings screen works exactly like one set as a variable.
/// </para>
/// </remarks>
internal static class ObservabilityExtensions
{
    internal const string ApiService = "culina-api";

    /// <summary>The service what the web app reported is exported as.</summary>
    internal const string WebAppService = "culina-web";

    internal static IHostApplicationBuilder AddObservability(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureLogging();

        var telemetry = builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddCulinaService(ApiService, builder.Environment).AddMachine())
            .WithTracing(ConfigureTracing)
            .WithMetrics(ConfigureMetrics);

        if (Exports(builder.Configuration))
        {
            telemetry.UseOtlpExporter();
        }

        return builder;
    }

    /// <summary>
    /// Only when a collector is configured. Without this guard the exporter
    /// retries against localhost forever and fills the log with noise.
    /// </summary>
    private static bool Exports(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration[TelemetrySettings.EndpointKey]);

    private static ResourceBuilder AddCulinaService(
        this ResourceBuilder resource,
        string serviceName,
        IHostEnvironment environment) =>
        resource
            .AddService(
                serviceName: serviceName,
                serviceVersion: Version(),
                serviceInstanceId: Environment.MachineName)
            .AddAttributes([
                new KeyValuePair<string, object>("deployment.environment.name", environment.EnvironmentName)
            ]);

    /// <summary>
    /// The machine the server runs on, on every span, metric and line it
    /// exports: which host and container, which operating system and runtime,
    /// and which process. Not the web app's, whose machine is the browser's.
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

        // Every line carries the trace and span ids, so the lines of a request
        // are correlated, and so are those of the work no request carries: an
        // import, a sweep, a migration. The trace id is the request id a user
        // is shown.
        builder.Logging.Configure(options =>
            options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);

        // The host opens a scope naming every request's raw path, and so put a
        // share token or invitation code on every line a request to one wrote.
        // It only opens it while this category logs at all; its own lines are
        // the request start and finish the request line already covers, with
        // the path redacted, and the trace id joins every other line to it.
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
            // JSON in production so a collector can parse fields instead of
            // regex-ing a message.
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

        // What the web app reported is exported as the web app, not as the
        // API that passed it on. It still goes to the console with the rest.
        builder.Logging.AddFilter<OpenTelemetryLoggerProvider>(CulinaTelemetry.WebAppCategory, LogLevel.None);
        builder.Services.AddSingleton<ILoggerProvider>(services =>
        {
            var configuration = services.GetRequiredService<IConfiguration>();

            return new WebAppLoggerProvider(
                configuration,
                resource => resource.AddCulinaService(WebAppService, builder.Environment),
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
                // Health checks are most of the traffic and none of the
                // information.
                options.Filter = context => !context.Request.Path.StartsWithSegments("/health");

                // Without the share token or invitation code a path can carry:
                // whoever reads the collector must not be handed what it opens.
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
            // CPU time, memory and threads of the process as the operating
            // system counts them, beside the runtime's own view of its heap.
            .AddProcessInstrumentation()
            .AddMeter(MachineMetrics.MeterName)
            .AddInstrumentation(services => new MachineMetrics(services.GetService<StorageSettings>()))
            // Npgsql publishes its pool and command metrics on its own meter
            // rather than through an instrumentation package.
            .AddMeter("Npgsql");

    internal static string Version() =>
        typeof(ObservabilityExtensions).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";
}
