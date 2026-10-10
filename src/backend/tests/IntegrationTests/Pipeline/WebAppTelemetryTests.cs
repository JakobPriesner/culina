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

/// <summary>The web app's records reach the collector through the API but are filed under the web app's service.</summary>
public class WebAppTelemetryTests
{
    [Fact]
    public void WebAppRecords_ShouldBeExportedAsTheWebApp_WithTheRequestScope()
    {
        var exported = new Exported();

        using var provider = new WebAppLoggerProvider(
            new ConfigurationBuilder().Build(),
            resource => resource.AddWebAppService(
                new ConfigurationBuilder().Build(),
                Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings()).Environment),
            logging => logging.AddProcessor(exported));
        using var loggers = LoggerFactory.Create(logging => logging.AddProvider(provider));

        using (loggers.CreateLogger(CulinaTelemetry.WebAppCategory)
                   .BeginScope(new Dictionary<string, object> { ["RequestId"] = "request-1" }))
        {
            loggers.CreateLogger(CulinaTelemetry.WebAppCategory).LogError("The page broke");
        }

        loggers.CreateLogger("Api.Anything").LogError("The server broke");

        var record = Assert.Single(exported.Records);
        Assert.Equal(CulinaTelemetry.WebAppCategory, record.Category);
        Assert.Equal(ObservabilityExtensions.WebAppService, record.Service);
        Assert.Contains("request-1", record.Scopes);
        Assert.Null(record.Version);
    }

    [Fact]
    public void WebAppRecords_ShouldNotBeExportedAsTheApi()
    {
        var exported = new Exported();
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());

        builder.AddObservability();
        builder.Services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddProcessor(exported));

        using var host = builder.Build();
        var loggers = host.Services.GetRequiredService<ILoggerFactory>();

        loggers.CreateLogger(CulinaTelemetry.WebAppCategory).LogError("The page broke");
        loggers.CreateLogger("Api.Anything").LogError("The server broke");

        var record = Assert.Single(exported.Records);
        Assert.Equal("Api.Anything", record.Category);
        Assert.Equal(ObservabilityExtensions.ApiService, record.Service);
        Assert.Single(host.Services.GetServices<ILoggerProvider>().OfType<WebAppLoggerProvider>());
    }

    /// <summary>compose.prod.yaml and .env.example set culina-api, which must still pair with culina-web.</summary>
    [Theory]
    [InlineData(null, "culina-api", "culina-web")]
    [InlineData("  ", "culina-api", "culina-web")]
    [InlineData("culina-api", "culina-api", "culina-web")]
    [InlineData("culina-staging-api", "culina-staging-api", "culina-staging-web")]
    [InlineData(" culina-staging ", "culina-staging", "culina-staging-web")]
    public void ServiceNames_ShouldFollowOtelServiceName(string? configured, string api, string web)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { [ObservabilityExtensions.ServiceNameKey] = configured }).Build();

        Assert.Equal(api, ObservabilityExtensions.ApiServiceName(configuration));
        Assert.Equal(web, ObservabilityExtensions.WebAppServiceName(configuration));
    }

    [Fact]
    public void Records_ShouldUseOtelServiceName_ForTheApiAndItsWebSuffixForTheWebApp()
    {
        var exported = new Exported();
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration[ObservabilityExtensions.ServiceNameKey] = "culina-staging";

        builder.AddObservability();
        builder.Services.ConfigureOpenTelemetryLoggerProvider(logging => logging.AddProcessor(exported));

        using var host = builder.Build();
        host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Api.Anything").LogError("The server broke");

        Assert.Equal("culina-staging", Assert.Single(exported.Records).Service);

        var webExported = new Exported();
        using var provider = new WebAppLoggerProvider(
            builder.Configuration,
            resource => resource.AddWebAppService(builder.Configuration, builder.Environment),
            logging => logging.AddProcessor(webExported));
        using var loggers = LoggerFactory.Create(logging => logging.AddProvider(provider));
        loggers.CreateLogger(CulinaTelemetry.WebAppCategory).LogError("The page broke");

        Assert.Equal("culina-staging-web", Assert.Single(webExported.Records).Service);
    }

    private sealed record ExportedRecord(string? Category, string? Service, string? Version, IReadOnlyList<string> Scopes);

    private sealed class Exported : BaseProcessor<LogRecord>
    {
        public List<ExportedRecord> Records { get; } = [];

        public override void OnEnd(LogRecord data)
        {
            var service = ParentProvider?.GetResource().Attributes
                .FirstOrDefault(attribute => attribute.Key == "service.name").Value as string;
            var version = ParentProvider?.GetResource().Attributes
                .FirstOrDefault(attribute => attribute.Key == "service.version").Value as string;
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

            Records.Add(new ExportedRecord(data.CategoryName, service, version, scopes));
        }
    }
}
