using Application.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace Api.Infrastructure;

/// <summary>
/// Exports what the web app reported as a service of its own.
/// </summary>
/// <remarks>
/// <para>
/// The browser's records reach the collector through the API, which logs them
/// for it. On the host's own provider they would carry the API's resource, and
/// a page that broke would be filed under the server. A resource belongs to a
/// whole provider rather than to a record, so the web app gets a second one,
/// and the host's provider leaves its category alone.
/// </para>
/// <para>
/// It shares the host's scopes, so the request id and the user id still come
/// along; the trace comes from the current activity either way.
/// </para>
/// </remarks>
internal sealed class WebAppLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ServiceProvider services;
    private readonly OpenTelemetryLoggerProvider exporting;

    /// <param name="configuration">Where the exporter reads the <c>OTEL_*</c> names from.</param>
    /// <param name="resource">Names the service the records are exported as.</param>
    /// <param name="export">Adds the exporter, or nothing when no collector is configured.</param>
    internal WebAppLoggerProvider(
        IConfiguration configuration,
        Action<ResourceBuilder> resource,
        Action<LoggerProviderBuilder> export)
    {
        var collection = new ServiceCollection().AddSingleton(configuration);

        collection
            .AddOpenTelemetry()
            .ConfigureResource(resource)
            .WithLogging(export, options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
            });

        services = collection.BuildServiceProvider();
        exporting = services.GetServices<ILoggerProvider>().OfType<OpenTelemetryLoggerProvider>().Single();
    }

    public ILogger CreateLogger(string categoryName) =>
        categoryName == CulinaTelemetry.WebAppCategory
            ? exporting.CreateLogger(categoryName)
            : NullLogger.Instance;

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) =>
        ((ISupportExternalScope)exporting).SetScopeProvider(scopeProvider);

    // Flushes whatever the batch still holds. The container would dispose the
    // provider too; doing it first says the flush is the point.
    public void Dispose()
    {
        exporting.Dispose();
        services.Dispose();
    }
}
