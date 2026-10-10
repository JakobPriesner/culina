using Application.Abstractions;
using Application.Abstractions.Settings;
using Domain.Suggestions;
using Infrastructure.Assistance;
using Infrastructure.Identity;
using Infrastructure.Import;
using Infrastructure.Import.Tandoor;
using Infrastructure.Nutrition;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Assistance;
using Infrastructure.Persistence.Cookbooks;
using Infrastructure.Persistence.Cooking;
using Infrastructure.Persistence.Households;
using Infrastructure.Persistence.Import;
using Infrastructure.Persistence.Migrations;
using Infrastructure.Persistence.Planning;
using Infrastructure.Persistence.Recipes;
using Infrastructure.Persistence.Searches;
using Infrastructure.Persistence.Shopping;
using Infrastructure.Persistence.Suggestions;
using Infrastructure.Persistence.Tags;
using Infrastructure.Persistence.Trash;
using Infrastructure.Persistence.Users;
using Infrastructure.Settings;
using Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Infrastructure;

/// <summary>Registers every adapter explicitly; no assembly scanning, so a missing service is a compile error.</summary>
public static class DependencyInjection
{
    /// <summary>Adds settings, persistence and the adapters built on them.</summary>
    /// <param name="services">The container.</param>
    /// <param name="configuration">The configuration to read settings from.</param>
    /// <param name="environment">The host environment.</param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // First, so a misconfigured process fails at startup.
        services.AddBootstrapSettings(configuration, environment);

        DapperConfiguration.Apply();

        // Order matters: hosted services start in registration order, and the settings loader reads a table the migration runner creates.
        return services
            .AddPersistence()
            .AddServerSettings()
            .AddScoped<ISetupProgress, AccountSetupProgress>()
            .AddInstanceSettings()
            .AddIdentity()
            .AddRecipeImport()
            .AddAssistance()
            .AddScoped<IRecipeIntakeJobs, RecipeIntakeJobs>()
            .AddSingleton<PushTransport>()
            .AddScoped<IIntakeNotifications, IntakeNotifications>()
            .AddHostedService<RecipeIntakeWorker>()
            .AddSingleton<IImageStore, FileSystemImageStore>()
            .AddSingleton(TimeProvider.System);
    }

    /// <summary>Adds what the pre-database setup host needs; nothing here may assume a database exists.</summary>
    /// <param name="services">The container.</param>
    public static IServiceCollection AddSetupInfrastructure(this IServiceCollection services) =>
        services
            .AddServerSettings()
            .AddSingleton<ISetupProgress, DatabaseSetupProgress>()
            .AddSingleton(TimeProvider.System);

    private static IServiceCollection AddServerSettings(this IServiceCollection services) =>
        services
            .AddSingleton<IServerConfiguration, ServerConfiguration>()
            .AddSingleton<IDatabaseConnectionCheck, DatabaseConnectionCheck>();

    private static IServiceCollection AddInstanceSettings(this IServiceCollection services) =>
        services
            .AddSingleton<RegistrationSettings>()
            .AddScoped<ISettingsStore<RegistrationSettings>, PostgresSettingsStore<RegistrationSettings>>()
            .AddSingleton<AssistanceSettings>()
            .AddScoped<ISettingsStore<AssistanceSettings>, PostgresSettingsStore<AssistanceSettings>>()
            .AddHostedService<InstanceSettingsLoader>();

    // Adapters are singletons (stateless, key and model read per call, shared HTTP pool); the ledger is scoped to write through the request's connection.
    private static IServiceCollection AddAssistance(this IServiceCollection services) =>
        services
            .AddSingleton<ISecretProtector, SecretProtector>()
            .AddSingleton<AssistantHttp>()
            .AddSingleton<IAssistant, GeminiAssistant>()
            .AddSingleton<IAssistant, OpenAiAssistant>()
            .AddSingleton<IAssistant, OllamaAssistant>()
            .AddSingleton<IAssistants, Assistants>()
            .AddSingleton<IModelPrices, ModelPrices>()
            .AddScoped<IAssistanceLedger, AssistanceLedger>();

    // Readers are singletons (stateless, shared HTTP pool); one per source app, picked by the registry.
    private static IServiceCollection AddRecipeImport(this IServiceCollection services) =>
        services
            .AddSingleton<SourceHttp>()
            // Source tokens use their own encryption purpose so they cannot be read back as the assistant's key, or vice versa.
            .AddKeyedSingleton<ISecretProtector>(
                SecretProtector.SourceTokens,
                (provider, _) => new SecretProtector(
                    provider.GetRequiredService<StorageSettings>(), SecretProtector.SourceTokens))
            .AddSingleton<IRecipeLibrary, TandoorLibrary>()
            .AddSingleton<IRecipeLibraries, RecipeLibraries>()
            .AddHostedService<ImportWorker>();

    private static IServiceCollection AddIdentity(this IServiceCollection services) =>
        services
            // One instance, so its cap on simultaneous hashes covers every request.
            .AddSingleton<IPasswordHasher, Argon2PasswordHasher>()
            .AddSingleton<ISecretTokens, SecretTokens>()
            .AddScoped<ISessionStore, SessionStore>()
            // Singleton: attempt counters are shared across requests.
            .AddSingleton<ILoginAttempts, InMemoryLoginAttempts>()
            .AddHostedService<ExpiredSessionSweeper>();

    private static IServiceCollection AddPersistence(this IServiceCollection services) =>
        services
            // The data source owns the connection pool: exactly one per process.
            .AddSingleton(provider =>
                CulinaDataSource.Build(provider.GetRequiredService<DatabaseSettings>()))
            // Singleton: the minute between reports holds across requests.
            .AddSingleton<ConnectionPoolWatch>()
            .AddScoped<DbSession>()
            .AddScoped<DbExecutor>()
            .AddScoped<IUnitOfWork, UnitOfWork>()
            .AddScoped<IDatabaseProbe, DatabaseProbe>()
            .AddScoped<IUserRepository, UserRepository>()
            .AddScoped<IUserPreferencesRepository, UserPreferencesRepository>()
            .AddScoped<IRecoveryCodeRepository, RecoveryCodeRepository>()
            .AddScoped<ITrashRepository, TrashRepository>()
            .AddScoped<IHouseholdRepository, HouseholdRepository>()
            .AddScoped<IInvitationRepository, InvitationRepository>()
            .AddScoped<TagWriter>()
            .AddScoped<RecipeSearcher>()
            .AddScoped<SearchDocumentWriter>()
            .AddScoped<ISearchVocabulary, SearchVocabulary>()
            .AddScoped<IRelatedRecipes, RelatedRecipes>()
            .AddScoped<ILookalikeRecipes, LookalikeRecipes>()
            .AddScoped<IRecipeRepository, RecipeRepository>()
            .AddScoped<IRecipeShareRepository, RecipeShareRepository>()
            .AddScoped<IPersonalNoteRepository, PersonalNoteRepository>()
            .AddScoped<ICookLogRepository, CookLogRepository>()
            .AddScoped<ICookSessionRepository, CookSessionRepository>()
            .AddSingleton<IWebPageFetcher, SafeWebPageFetcher>()
            .AddScoped<IMealPlanRepository, MealPlanRepository>()
            .AddScoped<ICookbookRepository, CookbookRepository>()
            .AddScoped<ISavedSearchRepository, SavedSearchRepository>()
            .AddScoped<ITagRepository, TagRepository>()
            .AddScoped<IShoppingListRepository, ShoppingListRepository>()
            .AddScoped<IRecipeSourceRepository, RecipeSourceRepository>()
            .AddScoped<IRecipeOriginRepository, RecipeOriginRepository>()
            // Ranking weights are code, not configuration, so installations behave identically; a singleton so tests can vary one.
            .AddSingleton(RankingWeights.Default)
            .AddScoped<SuggestionReader>()
            .AddScoped<ISuggestionRanker, SuggestionRanker>()
            .AddScoped<ISuggestionFeedback, SuggestionFeedbackRepository>()
            .AddScoped<MigrationRunner>()
            // Hosted, so the schema is current before the first request and a failed migration stops the process.
            .AddHostedService<MigrationHostedService>()
            // After the migrations: encrypts source tokens stored before encryption existed.
            .AddHostedService<RecipeSourceTokenEncryption>()
            // After the migrations: re-indexes documents built by an older lexicon before the first request.
            .AddHostedService<LexiconReindexService>()
            // After the migrations: it reads tables they renamed.
            .AddHostedService<TrashPurger>();
}
