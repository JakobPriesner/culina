using Application.Abstractions.Settings;
using Domain.Suggestions;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace IntegrationTests.Fixtures;

/// <summary>One real PostgreSQL server for the whole test run.</summary>
/// <remarks>
/// A container, not a shared developer database: the schema comes from the actual migrations, tests
/// cannot disturb local data, and CI needs only Docker.
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string DatabaseName = "culina";
    private const string RoleName = "culina_app";
    private const string RolePassword = "culina_test_password";

    // The container's own user is the superuser, as in production; the app
    // connects as a role created from it below.
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase(DatabaseName)
        .Build();

    private CulinaApiFactory? api;
    private CulinaApiFactory? steadyRanking;
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

    /// <summary>The same server as its superuser, which Culina must refuse to run as.</summary>
    public DatabaseSettings SuperuserSettings
    {
        get
        {
            var own = new NpgsqlConnectionStringBuilder(container.GetConnectionString());

            return Settings with { Username = own.Username!, Password = own.Password! };
        }
    }

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();

        // What scripts/db-init.sh does in production: an ordinary role that owns the schema, so a
        // migration that quietly needs a superuser fails here, not on someone's server.
        await ExecuteAsSuperuserAsync(
            $"""
            CREATE ROLE {RoleName} LOGIN PASSWORD '{RolePassword}';
            GRANT ALL ON DATABASE {DatabaseName} TO {RoleName};
            ALTER SCHEMA public OWNER TO {RoleName};
            """,
            CancellationToken.None);
    }

    /// <summary>
    /// Runs SQL as the server's superuser, for arranging what the application role may not do
    /// itself, such as creating a database.
    /// </summary>
    public async Task ExecuteAsSuperuserAsync(string sql, CancellationToken cancellationToken)
    {
        var result = await container.ExecScriptAsync(sql, cancellationToken);

        if (result.ExitCode != 0 || result.Stderr.Contains("ERROR", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Superuser SQL failed: {result.Stderr}");
        }
    }

    /// <summary>
    /// The API host, created once and shared; starting it runs the migrations, so the schema is
    /// built once per run.
    /// </summary>
    public CulinaApiFactory Api => api ??= new CulinaApiFactory(this, servesIntakes: true);

    /// <summary>The same host, ranking without its exploration jitter.</summary>
    /// <remarks>
    /// The jitter moves a recipe for reasons unrelated to any ordering rule, and a rule asserted
    /// against noise larger than its signal passes most of the time. Turning it off is how a weight
    /// gets proven.
    /// </remarks>
    public CulinaApiFactory SteadyRanking => steadyRanking ??= new CulinaApiFactory(
        this,
        weights: RankingWeights.Default with { Exploration = 0m });

    /// <summary>
    /// A connection for a test, from the one pool this fixture owns; a data source per test would
    /// leak a pool per test.
    /// </summary>
    internal DbSession NewSession() => SessionOn(DataSource);

    /// <summary>
    /// A session on a pool the test chose, built as the app builds one but reporting its pool use
    /// to nobody.
    /// </summary>
    internal static DbSession SessionOn(NpgsqlDataSource pool) =>
        new(pool, new ConnectionPoolWatch(TimeProvider.System, NullLogger<ConnectionPoolWatch>.Instance));

    /// <summary>Runs one statement against the instance's database.</summary>
    /// <remarks>
    /// For arranging states the API has no way to produce (an expired invitation, a membership
    /// removed under a live session).
    /// </remarks>
    public async Task ExecuteAsync(string sql, CancellationToken cancellationToken)
    {
        var connection = await DataSource.OpenConnectionAsync(cancellationToken);

        await using (connection.ConfigureAwait(false))
        {
            var command = connection.CreateCommand();

            await using (command.ConfigureAwait(false))
            {
                // Test source only: the SQL is never composed from anything a caller supplied.
#pragma warning disable CA2100
                command.CommandText = sql;
#pragma warning restore CA2100

                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    /// <summary>Runs an embedded migration's statements again, to prove what it repairs in data it was not applied to.</summary>
    /// <remarks>Only for idempotent data migrations: the schema was built by the host, which already ran it.</remarks>
    public async Task RerunMigrationAsync(string version, CancellationToken cancellationToken) =>
        await ExecuteAsync(
            EmbeddedMigrations.Load().Single(one => one.Version == version).Sql,
            cancellationToken);

    /// <summary>
    /// Built only once the host has migrated: a pool opened earlier learns the server's types too
    /// soon and cannot read a citext column.
    /// </summary>
    private NpgsqlDataSource DataSource
    {
        get
        {
            if (dataSource is null)
            {
                _ = Api.Services;
                dataSource = CulinaDataSource.Build(Settings);
            }

            return dataSource;
        }
    }

    /// <summary>
    /// Reads one value straight from the database, for asserting on what the API hides.
    /// </summary>
    public async Task<TValue> QuerySingleAsync<TValue>(string sql, CancellationToken cancellationToken)
    {
        var connection = await DataSource.OpenConnectionAsync(cancellationToken);

        await using (connection.ConfigureAwait(false))
        {
            var command = connection.CreateCommand();

            await using (command.ConfigureAwait(false))
            {
#pragma warning disable CA2100
                command.CommandText = sql;
#pragma warning restore CA2100

                return (TValue)(await command.ExecuteScalarAsync(cancellationToken))!;
            }
        }
    }

    /// <summary>
    /// Returns the instance to a clean state: every table empty, every settings group at its
    /// compiled-in defaults.
    /// </summary>
    /// <remarks>
    /// Settings are a process-wide mutable singleton, so a test that opens registration would leak
    /// it into every later test.
    /// </remarks>
    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        await DatabaseReset.TruncateAllAsync(DataSource, cancellationToken);

        if (api is not null)
        {
            api.Services.GetRequiredService<RegistrationSettings>()
                .CopyFrom(new RegistrationSettings());

            api.Services.GetRequiredService<AssistanceSettings>()
                .CopyFrom(new AssistanceSettings());
        }
    }

    public async ValueTask DisposeAsync()
    {
        api?.Dispose();
        steadyRanking?.Dispose();

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
