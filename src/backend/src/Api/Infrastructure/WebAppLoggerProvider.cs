using Application.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace Api.Infrastructure;

/// <summary>Exports what the web app reported as a service of its own.</summary>
/// <remarks>
/// A resource belongs to a whole provider, so the web app gets a second one (the host's would file
/// page errors under the server). It shares the host's scopes, so request and user ids still come
/// along.
/// </remarks>
internal sealed class WebAppLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ServiceProvider services;
    private readonly OpenTelemetryLoggerProvider exporting;

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

    // Flushes whatever the batch still holds. The container would dispose the provider too; doing
    // it first says the flush is the point.
    public void Dispose()
    {
        exporting.Dispose();
        services.Dispose();
    }
}
