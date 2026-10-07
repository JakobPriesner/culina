using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Settings;

/// <summary>Registers every bootstrap settings group, one explicit line each.</summary>
/// <remarks>
/// Called first in <c>AddInfrastructure</c>, so a misconfigured deployment fails during startup,
/// not on the first request that needed a value.
/// </remarks>
public static class BootstrapSettingsExtensions
{
    /// <summary>Adds all bootstrap settings records as validated singletons.</summary>
    public static IServiceCollection AddBootstrapSettings(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment) =>
        services
            .AddDatabaseSettings(configuration)
            .AddStorageSettings(configuration)
            .AddCookieSettings(configuration, environment)
            .AddPasswordHashingSettings(configuration)
            .AddRateLimitSettings(configuration)
            .AddForwardedHeadersSettings(configuration)
            .AddImportSettings(configuration)
            .AddSiteSettings(configuration)
            .AddTelemetrySettings(configuration);
}
