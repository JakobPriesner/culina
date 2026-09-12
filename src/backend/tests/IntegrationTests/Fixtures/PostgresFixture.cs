using Application.Abstractions.Settings;
using Infrastructure.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;

namespace IntegrationTests.Fixtures;

/// <summary>
/// One real PostgreSQL server for the whole test run.
/// </summary>
/// <remarks>
/// A container rather than a shared developer database: the schema is built
/// from the actual migrations, the tests cannot disturb anyone's local data,
/// and CI needs nothing installed but Docker.
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string DatabaseName = "culina";
    private const string RoleName = "culina_app";
    private const string RolePassword = "culina_test_password";

    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase(DatabaseName)
        .WithUsername(RoleName)
        .WithPassword(RolePassword)
        .Build();

    private CulinaApiFactory? api;
    private NpgsqlDataSource? dataSource;

    /// <summary>How to reach the container, in the shape the app configures.</summary>
    public DatabaseSettings Settings => new()
    {
        Host = container.Hostname,
        Port = container.GetMappedPublicPort(5432),
        Name = DatabaseName,
        Username = RoleName,
        Password = RolePassword,
        RequireSsl = false
    };

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();

        // citext, pg_trgm and unaccent need superuser rights, so in production
        // the database's init script installs them rather than a migration.
        // Creating them here gives the tests the same starting state.
        await container.ExecScriptAsync(
            """
            CREATE EXTENSION IF NOT EXISTS citext;
            CREATE EXTENSION IF NOT EXISTS pg_trgm;
            CREATE EXTENSION IF NOT EXISTS unaccent;
            """);
    }

    /// <summary>
    /// The API host, created once and shared. Starting it runs the migrations,
    /// so the schema is built once per run rather than once per test class.
    /// </summary>
    public CulinaApiFactory Api => api ??= new CulinaApiFactory(this);

    /// <summary>
    /// A connection for a test, from the one pool this fixture owns. Building a
    /// data source per test would leak a pool per test.
    /// </summary>
    internal DbSession NewSession() => new(DataSource);

    private NpgsqlDataSource DataSource => dataSource ??= CulinaDataSource.Build(Settings);

    /// <summary>Empties every table, leaving the migrated schema in place.</summary>
    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        await DatabaseReset.TruncateAllAsync(DataSource, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        api?.Dispose();

        if (dataSource is not null)
        {
            await dataSource.DisposeAsync();
        }

        await container.DisposeAsync();
    }
}

/// <summary>Shares one container across every test class that needs a database.</summary>
[CollectionDefinition(RequiresDatabase.Name)]
public sealed class RequiresDatabase : ICollectionFixture<PostgresFixture>
{
    public const string Name = "database";
}
