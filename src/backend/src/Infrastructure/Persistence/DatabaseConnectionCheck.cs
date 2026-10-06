using System.Net.Sockets;
using Application.Abstractions.Settings;
using Domain.Shared;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.Persistence;

/// <summary>
/// Connects with a proposed set of details, on a pool of its own that is
/// thrown away afterwards, and checks the things the migrations will need.
/// </summary>
/// <param name="logger">Where the reason a connection failed is written.</param>
/// <remarks>
/// It connects to whatever host and port it is given, during setup for anyone
/// at all. So the caller learns only which kind of failure it was, never the
/// socket's or the server's own words — those would answer "is anything
/// listening there, and what" for any address — and the words go to the log,
/// with the address, for the operator.
/// </remarks>
internal sealed class DatabaseConnectionCheck(ILogger<DatabaseConnectionCheck> logger) : IDatabaseConnectionCheck
{
    /// <summary>
    /// What the schema is built on. They are trusted extensions, so the first
    /// migration installs them as the application role — provided it may
    /// create things in the database.
    /// </summary>
    private static readonly string[] Extensions = ["citext", "pg_trgm", "unaccent"];

    public async Task<Result> CheckAsync(DatabaseSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var dataSource = CulinaDataSource.Build(settings);

        await using (dataSource.ConfigureAwait(false))
        {
            try
            {
                var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

                await using (connection.ConfigureAwait(false))
                {
                    return await SuitabilityAsync(connection, settings, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (NpgsqlException failure)
            {
                // The answer this check exists to give, not a defect: a wrong
                // password, a host that does not resolve, a server that is not
                // listening.
                var error = Category(failure, settings);

                logger.CheckFailed(settings.Host, settings.Port, error.Code, Reason(failure));

                return error;
            }
        }
    }

    private async Task<Result> SuitabilityAsync(
        NpgsqlConnection connection,
        DatabaseSettings settings,
        CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(
            """
            select
                (select rolsuper from pg_roles where rolname = current_user) as superuser,
                array(select extname from pg_extension where extname = any(@extensions)) as installed,
                has_database_privilege(current_database(), 'CREATE') as may_install,
                has_schema_privilege('public', 'CREATE') as may_create;
            """,
            connection);

        await using (command.ConfigureAwait(false))
        {
            command.Parameters.AddWithValue("extensions", Extensions);

            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            await using (reader.ConfigureAwait(false))
            {
                await reader.ReadAsync(cancellationToken).ConfigureAwait(false);

                var superuser = await reader.GetFieldValueAsync<bool>(0, cancellationToken).ConfigureAwait(false);
                var installed = await reader.GetFieldValueAsync<string[]>(1, cancellationToken).ConfigureAwait(false);
                var mayInstall = await reader.GetFieldValueAsync<bool>(2, cancellationToken).ConfigureAwait(false);
                var mayCreate = await reader.GetFieldValueAsync<bool>(3, cancellationToken).ConfigureAwait(false);
                var missing = Extensions.Except(installed).ToList();

                // First: a superuser may do everything below, which is exactly
                // why it is refused. See SettingsErrors.DatabaseSuperuser.
                if (superuser)
                {
                    logger.CheckFailed(
                        settings.Host,
                        settings.Port,
                        SettingsErrors.DatabaseSuperuser.Code,
                        $"the role {settings.Username} is a superuser");

                    return SettingsErrors.DatabaseSuperuser;
                }

                if (missing.Count > 0 && !mayInstall)
                {
                    return SettingsErrors.DatabaseUnsuitable(
                        $"the role {settings.Username} may not install the {string.Join(", ", missing)} "
                        + $"extension{(missing.Count == 1 ? "" : "s")} "
                        + $"(as a superuser: GRANT CREATE ON DATABASE {settings.Name} TO {settings.Username};)");
                }

                return mayCreate
                    ? Result.Success()
                    : SettingsErrors.DatabaseUnsuitable(
                        $"the role {settings.Username} may not create tables in the public schema "
                        + $"(as a superuser: ALTER SCHEMA public OWNER TO {settings.Username};)");
            }
        }
    }

    /// <summary>Which kind of failure it was, and nothing more.</summary>
    /// <remarks>
    /// The TLS case is told apart by Npgsql's own message, since it carries no
    /// type of its own. Should that wording change, the failure falls through
    /// to "not a usable server" — less helpful, never more revealing.
    /// </remarks>
    private static Error Category(NpgsqlException failure, DatabaseSettings settings) => failure switch
    {
        PostgresException { SqlState: PostgresErrorCodes.InvalidPassword }
            or PostgresException { SqlState: PostgresErrorCodes.InvalidAuthorizationSpecification }
            or PostgresException { SqlState: PostgresErrorCodes.InvalidCatalogName } => SettingsErrors.DatabaseLoginRefused,
        PostgresException => SettingsErrors.DatabaseNotPostgres,
        { InnerException: SocketException or TimeoutException } => SettingsErrors.DatabaseUnreachable,
        _ when settings.RequireSsl && failure.Message.Contains("SSL", StringComparison.Ordinal) =>
            SettingsErrors.DatabaseTlsFailed,
        _ => SettingsErrors.DatabaseNotPostgres
    };

    /// <summary>What the attempt reported, for the log. Never carries the password.</summary>
    private static string Reason(NpgsqlException failure) => failure switch
    {
        PostgresException postgres => $"{postgres.SqlState}: {postgres.MessageText}",
        { InnerException: { } inner } => $"{failure.Message}: {inner.Message}",
        _ => failure.Message
    };
}
