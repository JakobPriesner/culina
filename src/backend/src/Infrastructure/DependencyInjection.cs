using Application.Abstractions;
using Application.Abstractions.Settings;
using Domain.Suggestions;
using Infrastructure.Assistance;
using Infrastructure.Identity;
using Infrastructure.Import;
using Infrastructure.Import.Tandoor;
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
using Infrastructure.Persistence.Users;
using Infrastructure.Settings;
using Infrastructure.Storage;
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
            .AddRecipeImport()
            .AddAssistance()
            .AddSingleton<IImageStore, FileSystemImageStore>()
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
            .AddSingleton<AssistanceSettings>()
            .AddScoped<ISettingsStore<AssistanceSettings>, PostgresSettingsStore<AssistanceSettings>>()
            .AddHostedService<InstanceSettingsLoader>();

    /// <summary>
    /// The assistant: one adapter per provider, the registry that picks between
    /// them, and the ledger that decides whether there is budget to call one.
    /// </summary>
    /// <remarks>
    /// The adapters are singletons because they hold nothing per request — the
    /// key and the model are read from the settings singleton at each call, so
    /// rotating a key takes effect immediately — and because the client
    /// underneath them owns a connection pool that must not be rebuilt per
    /// request. The ledger is scoped: it writes through the request's own
    /// database connection.
    /// </remarks>
    private static IServiceCollection AddAssistance(this IServiceCollection services) =>
        services
            .AddSingleton<ISecretProtector, SecretProtector>()
            .AddSingleton<AssistantHttp>()
            .AddSingleton<IAssistant, GeminiAssistant>()
            .AddSingleton<IAssistant, OpenAiAssistant>()
            .AddSingleton<IAssistant, OllamaAssistant>()
            .AddSingleton<IAssistants, Assistants>()
            .AddScoped<IAssistanceLedger, AssistanceLedger>();

    /// <summary>
    /// Reading other people's recipe libraries.
    /// </summary>
    /// <remarks>
    /// One reader per app, and the registry that picks between them. Adding
    /// Mealie is one class and one line here; nothing in <c>Application</c>
    /// changes, which is what the <c>IRecipeLibrary</c> seam is for.
    ///
    /// The readers are singletons because they hold nothing per request — the
    /// connection and its token arrive as arguments — and because the HTTP
    /// client underneath them owns a connection pool that must not be rebuilt
    /// per request.
    /// </remarks>
    private static IServiceCollection AddRecipeImport(this IServiceCollection services) =>
        services
            .AddSingleton<SourceHttp>()
            .AddSingleton<IRecipeLibrary, TandoorLibrary>()
            .AddSingleton<IRecipeLibraries, RecipeLibraries>()
            // The background process that actually brings the recipes over. The
            // work it does is registered in Application; this is the lifetime
            // it runs on.
            .AddHostedService<ImportWorker>();

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
            .AddScoped<RecipeSearcher>()
            .AddScoped<SearchDocumentWriter>()
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
            // The ranking weights are code, not operator configuration: a
            // self-hosted app whose suggestions drift per installation is one
            // where two people comparing notes cannot reproduce each other's
            // behaviour and a bug report is untriageable. A singleton rather
            // than a static so a test can hold nine weights still and sweep the
            // tenth.
            .AddSingleton(RankingWeights.Default)
            .AddScoped<SuggestionReader>()
            .AddScoped<ISuggestionRanker, SuggestionRanker>()
            .AddScoped<ISuggestionFeedback, SuggestionFeedbackRepository>()
            .AddScoped<MigrationRunner>()
            // Hosted, so the schema is current before the first request and a
            // failed migration stops the process instead of serving traffic.
            .AddHostedService<MigrationHostedService>();
}
