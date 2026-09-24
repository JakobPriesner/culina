using System.Net.Sockets;
using Application.Abstractions.Settings;
using Domain.Shared;
using Npgsql;

namespace Infrastructure.Persistence;

/// <summary>
/// Connects with a proposed set of details, on a pool of its own that is
/// thrown away afterwards, and checks the things the migrations will need.
/// </summary>
internal sealed class DatabaseConnectionCheck : IDatabaseConnectionCheck
{
    /// <summary>
    /// What the schema is built on and the application role cannot install:
    /// <c>scripts/db-init.sh</c> creates them as a superuser.
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
                    return await SuitabilityAsync(connection, settings.Username, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (NpgsqlException failure)
            {
                // The answer this check exists to give, not a defect: a wrong
                // password, a host that does not resolve, a server that is not
                // listening. The message is the server's or the socket's own,
                // which is what someone typing the details needs to read, and
                // it never carries the password.
                return SettingsErrors.DatabaseUnreachable(Reason(failure));
            }
        }
    }

    private static async Task<Result> SuitabilityAsync(
        NpgsqlConnection connection,
        string username,
        CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(
            """
            select
                array(select extname from pg_extension where extname = any(@extensions)) as installed,
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

                var installed = await reader.GetFieldValueAsync<string[]>(0, cancellationToken).ConfigureAwait(false);
                var mayCreate = await reader.GetFieldValueAsync<bool>(1, cancellationToken).ConfigureAwait(false);
                var missing = Extensions.Except(installed).ToList();

                if (missing.Count > 0)
                {
                    return SettingsErrors.DatabaseUnsuitable(
                        $"it lacks the {string.Join(", ", missing)} extension{(missing.Count == 1 ? "" : "s")}, "
                        + "which only a superuser can create ("
                        + string.Join(" ", missing.Select(name => $"CREATE EXTENSION IF NOT EXISTS {name};"))
                        + ")");
                }

                return mayCreate
                    ? Result.Success()
                    : SettingsErrors.DatabaseUnsuitable(
                        $"the role {username} may not create tables in the public schema "
                        + $"(as a superuser: ALTER SCHEMA public OWNER TO {username};)");
            }
        }
    }

    private static string Reason(NpgsqlException failure) => failure switch
    {
        PostgresException postgres => postgres.MessageText,
        { InnerException: SocketException socket } => socket.Message,
        { InnerException: TimeoutException } => "the server did not answer in time",
        _ => failure.Message
    };
}
