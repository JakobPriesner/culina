using Application.Abstractions.Settings;
using Domain.Suggestions;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
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

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();

        // What scripts/db-init.sh does in production, and nothing more: an
        // ordinary role that owns the schema. The migrations then run as that
        // role, extensions included, so a migration that quietly needs a
        // superuser fails here rather than on someone's server.
        await ExecuteAsSuperuserAsync(
            $"""
            CREATE ROLE {RoleName} LOGIN PASSWORD '{RolePassword}';
            GRANT ALL ON DATABASE {DatabaseName} TO {RoleName};
            ALTER SCHEMA public OWNER TO {RoleName};
            """,
            CancellationToken.None);
    }

    /// <summary>
    /// Runs SQL as the server's superuser, for arranging what the application
    /// role may not do itself — creating a database, say.
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
    /// The API host, created once and shared. Starting it runs the migrations,
    /// so the schema is built once per run rather than once per test class.
    /// </summary>
    public CulinaApiFactory Api => api ??= new CulinaApiFactory(this);

    /// <summary>
    /// The same host, ranking without its exploration jitter.
    /// </summary>
    /// <remarks>
    /// The jitter is deliberate and worth having in the product: it is what
    /// keeps a list from being the same five recipes forever. It is also, by
    /// construction, a reason a recipe moves that has nothing to do with any
    /// ordering rule — and a rule asserted against noise larger than the rule's
    /// own signal is a test that passes most of the time. Turning it off is how
    /// a weight gets proven, which is what <c>RankingWeights</c> being a record
    /// is for.
    /// </remarks>
    public CulinaApiFactory SteadyRanking => steadyRanking ??= new CulinaApiFactory(
        this,
        weights: RankingWeights.Default with { Exploration = 0m });

    /// <summary>
    /// A connection for a test, from the one pool this fixture owns. Building a
    /// data source per test would leak a pool per test.
    /// </summary>
    internal DbSession NewSession() => new(DataSource);

    /// <summary>
    /// Runs one statement against the instance's database.
    /// </summary>
    /// <remarks>
    /// For arranging a state the API deliberately has no way to produce — an
    /// invitation that has already expired, a membership row removed under a
    /// live session. Reaching past the API to set those up is the only way to
    /// test what happens when they are true.
    /// </remarks>
    public async Task ExecuteAsync(string sql, CancellationToken cancellationToken)
    {
        var connection = await DataSource.OpenConnectionAsync(cancellationToken);

        await using (connection.ConfigureAwait(false))
        {
            var command = connection.CreateCommand();

            await using (command.ConfigureAwait(false))
            {
                // The SQL here is written in test source, never composed from
                // anything a caller supplied.
#pragma warning disable CA2100
                command.CommandText = sql;
#pragma warning restore CA2100

                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }
    }

    /// <summary>
    /// Built only once the host has migrated: a pool learns the server's types
    /// on its first connection, and one opened before the first migration
    /// installed citext cannot read a citext column.
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
    /// Returns the instance to a clean state: every table empty, and every
    /// instance-settings group back at its compiled-in defaults.
    /// </summary>
    /// <remarks>
    /// Settings are deliberately a process-wide mutable singleton, so a test
    /// that opens registration would otherwise leak that into every test that
    /// runs afterwards. Resetting them here means no test has to remember.
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
