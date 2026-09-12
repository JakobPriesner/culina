using Application.Abstractions;
using Application.Abstractions.Settings;
using Infrastructure.Identity;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Households;
using Infrastructure.Persistence.Recipes;
using Infrastructure.Persistence.Migrations;
using Infrastructure.Persistence.Users;
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

        // Order matters here, and only here: hosted services start in
        // registration order, and the settings loader reads a table the
        // migration runner creates.
        return services
            .AddPersistence()
            .AddInstanceSettings()
            .AddIdentity()
            .AddSingleton(TimeProvider.System);
    }

    /// <summary>
    /// Admin-editable settings: one mutable singleton per group, its store, and
    /// the loader that fills them in before the first request.
    /// </summary>
    private static IServiceCollection AddInstanceSettings(this IServiceCollection services) =>
        services
            .AddSingleton<RegistrationSettings>()
            .AddScoped<ISettingsStore<RegistrationSettings>, PostgresSettingsStore<RegistrationSettings>>()
            .AddHostedService<InstanceSettingsLoader>();

    private static IServiceCollection AddIdentity(this IServiceCollection services) =>
        // Stateless and thread-safe, so one instance serves every request.
        services
            .AddSingleton<IPasswordHasher, Argon2PasswordHasher>()
            .AddSingleton<ISecretTokens, SecretTokens>()
            .AddScoped<ISessionStore, SessionStore>()
            // Singleton: the attempt counters must be shared across requests.
            .AddSingleton<ILoginAttempts, InMemoryLoginAttempts>()
            .AddHostedService<ExpiredSessionSweeper>();

    private static IServiceCollection AddPersistence(this IServiceCollection services) =>
        services
            // The data source owns the connection pool, so exactly one exists
            // for the process.
            .AddSingleton(provider =>
                CulinaDataSource.Build(provider.GetRequiredService<DatabaseSettings>()))
            // One connection per request, opened lazily.
            .AddScoped<DbSession>()
            .AddScoped<DbExecutor>()
            .AddScoped<IUnitOfWork, UnitOfWork>()
            .AddScoped<IDatabaseProbe, DatabaseProbe>()
            .AddScoped<IUserRepository, UserRepository>()
            .AddScoped<IUserPreferencesRepository, UserPreferencesRepository>()
            .AddScoped<IHouseholdRepository, HouseholdRepository>()
            .AddScoped<IInvitationRepository, InvitationRepository>()
            .AddScoped<TagWriter>()
            .AddScoped<IRecipeRepository, RecipeRepository>()
            .AddScoped<MigrationRunner>()
            // Hosted, so the schema is current before the first request and a
            // failed migration stops the process instead of serving traffic.
            .AddHostedService<MigrationHostedService>();
}
