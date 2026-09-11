using Application.Abstractions;
using Application.Abstractions.Settings;
using Infrastructure.Persistence;
using Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Infrastructure;

/// <summary>
/// Registers every adapter, explicitly and one line at a time.
/// </summary>
/// <remarks>
/// No assembly scanning. Scanning means a service can be deleted, renamed or
/// forgotten and the failure shows up as a 500 at runtime instead of a compile
/// error; it also defeats trimming. The cost is one line per service, and an
/// architecture test asserts that nothing is missing.
/// </remarks>
public static class DependencyInjection
{
    /// <summary>Adds settings, persistence and the adapters built on them.</summary>
    /// <param name="services">The container to register into.</param>
    /// <param name="configuration">The configuration to read settings from.</param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // First, so a misconfigured process fails during startup rather than on
        // the first request that needed a value.
        services.AddBootstrapSettings(configuration);

        DapperConfiguration.Apply();

        return services
            .AddPersistence()
            .AddSingleton(TimeProvider.System);
    }

    private static IServiceCollection AddPersistence(this IServiceCollection services) =>
        services
            // The data source owns the connection pool, so exactly one exists
            // for the process.
            .AddSingleton(provider =>
                CulinaDataSource.Build(provider.GetRequiredService<DatabaseSettings>()))
            // One connection per request, opened lazily.
            .AddScoped<DbSession>()
            .AddScoped<DbExecutor>()
            .AddScoped<IUnitOfWork, UnitOfWork>();
}
