using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Settings;

/// <summary>
/// Registers every bootstrap settings group, one explicit line each.
/// </summary>
/// <remarks>
/// Called before anything else in <c>AddInfrastructure</c>, so a misconfigured
/// deployment fails during startup rather than on the first request that needed
/// a value.
/// </remarks>
public static class BootstrapSettingsExtensions
{
    /// <summary>Adds all bootstrap settings records as validated singletons.</summary>
    /// <param name="services">The container to register into.</param>
    /// <param name="configuration">The configuration to read from.</param>
    public static IServiceCollection AddBootstrapSettings(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services
            .AddDatabaseSettings(configuration)
            .AddStorageSettings(configuration)
            .AddCookieSettings(configuration)
            .AddPasswordHashingSettings(configuration)
            .AddRateLimitSettings(configuration)
            .AddForwardedHeadersSettings(configuration);
}
