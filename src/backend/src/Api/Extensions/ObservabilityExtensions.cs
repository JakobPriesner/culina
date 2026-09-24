using System.Reflection;
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
    internal static IHostApplicationBuilder AddObservability(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureLogging();

        var telemetry = builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: "culina-api",
                    serviceVersion: Version(),
                    serviceInstanceId: Environment.MachineName)
                .AddAttributes([
                    new KeyValuePair<string, object>(
                        "deployment.environment.name",
                        builder.Environment.EnvironmentName)
                ]))
            .WithTracing(ConfigureTracing)
            .WithMetrics(ConfigureMetrics);

        // Only when a collector is configured. Without this guard the exporter
        // retries against localhost forever and fills the log with noise.
        if (!string.IsNullOrWhiteSpace(builder.Configuration[TelemetrySettings.EndpointKey]))
        {
            telemetry.UseOtlpExporter();
        }

        return builder;
    }

    private static void ConfigureLogging(this IHostApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();

        // Scopes carry the request id, so every line emitted while handling a
        // request is correlated.
        builder.Logging.Configure(options => options.ActivityTrackingOptions = ActivityTrackingOptions.None);

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
    }

    private static void ConfigureTracing(TracerProviderBuilder tracing) =>
        tracing
            .AddSource(CulinaTelemetry.Name)
            .AddAspNetCoreInstrumentation(options =>
                // Health checks are most of the traffic and none of the
                // information.
                options.Filter = context => !context.Request.Path.StartsWithSegments("/health"))
            .AddHttpClientInstrumentation()
            .AddNpgsql();

    private static void ConfigureMetrics(MeterProviderBuilder metrics) =>
        metrics
            .AddMeter(CulinaTelemetry.Name)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            // Npgsql publishes its pool and command metrics on its own meter
            // rather than through an instrumentation package.
            .AddMeter("Npgsql");

    private static string Version() =>
        typeof(ObservabilityExtensions).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";
}
