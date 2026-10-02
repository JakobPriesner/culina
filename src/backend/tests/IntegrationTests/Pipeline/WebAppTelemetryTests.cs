using Api.Extensions;
using Api.Infrastructure;
using Application.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;

namespace IntegrationTests.Pipeline;

/// <summary>
/// Which service the web app's records are exported as.
/// </summary>
/// <remarks>
/// They reach the collector through the API, and an operator looking at a
/// broken page should not have to know that: it is filed under the web app.
/// </remarks>
public class WebAppTelemetryTests
{
    [Fact]
    public void WebAppRecords_ShouldBeExportedAsTheWebApp_WithTheRequestScope()
    {
        // Arrange
        var exported = new Exported();

        using var provider = new WebAppLoggerProvider(
            new ConfigurationBuilder().Build(),
            resource => resource.AddService(ObservabilityExtensions.WebAppService),
            logging => logging.AddProcessor(exported));
        using var loggers = LoggerFactory.Create(logging => logging.AddProvider(provider));

        // Act
        using (loggers.CreateLogger(CulinaTelemetry.WebAppCategory)
                   .BeginScope(new Dictionary<string, object> { ["RequestId"] = "request-1" }))
        {
            loggers.CreateLogger(CulinaTelemetry.WebAppCategory).LogError("The page broke");
        }

        loggers.CreateLogger("Api.Anything").LogError("The server broke");

        // Assert
        var record = Assert.Single(exported.Records);
        Assert.Equal(CulinaTelemetry.WebAppCategory, record.Category);
        Assert.Equal(ObservabilityExtensions.WebAppService, record.Service);
        Assert.Contains("request-1", record.Scopes);
    }

    [Fact]
    public void WebAppRecords_ShouldNotBeExportedAsTheApi()
    {
        // Arrange
        var exported = new Exported();
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

        builder.AddObservability();
        builder.Services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddProcessor(exported));

        using var host = builder.Build();
        var loggers = host.Services.GetRequiredService<ILoggerFactory>();

        // Act
        loggers.CreateLogger(CulinaTelemetry.WebAppCategory).LogError("The page broke");
        loggers.CreateLogger("Api.Anything").LogError("The server broke");

        // Assert
        var record = Assert.Single(exported.Records);
        Assert.Equal("Api.Anything", record.Category);
        Assert.Equal(ObservabilityExtensions.ApiService, record.Service);
        Assert.Single(host.Services.GetServices<ILoggerProvider>().OfType<WebAppLoggerProvider>());
    }

    private sealed record ExportedRecord(string? Category, string? Service, IReadOnlyList<string> Scopes);

    private sealed class Exported : BaseProcessor<LogRecord>
    {
        public List<ExportedRecord> Records { get; } = [];

        public override void OnEnd(LogRecord data)
        {
            var service = ParentProvider?.GetResource().Attributes
                .FirstOrDefault(attribute => attribute.Key == "service.name").Value as string;
            var scopes = new List<string>();

            data.ForEachScope(
                (scope, collected) =>
                {
                    foreach (var item in scope)
                    {
                        collected.Add($"{item.Value}");
                    }
                },
                scopes);

            Records.Add(new ExportedRecord(data.CategoryName, service, scopes));
        }
    }
}
