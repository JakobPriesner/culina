using Application.Abstractions.Settings;
using Npgsql;

namespace Infrastructure.Persistence;

/// <summary>
/// Builds the pooled data source from the configured connection parts.
/// </summary>
internal static class CulinaDataSource
{
    internal static NpgsqlDataSource Build(DatabaseSettings settings)
    {
        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = settings.Host,
            Port = settings.Port,
            Database = settings.Name,
            Username = settings.Username,
            Password = settings.Password,
            SslMode = settings.RequireSsl ? SslMode.Require : SslMode.Disable,
            MaxPoolSize = settings.MaxPoolSize,
            // Named so a DBA looking at pg_stat_activity can tell which process
            // holds a connection.
            ApplicationName = "culina-api",
            // Fail fast rather than hang a request behind an unreachable host;
            // the health check and the retry both need to know quickly.
            Timeout = 10,
            CommandTimeout = 30
        }.ConnectionString;

        var builder = new NpgsqlDataSourceBuilder(connectionString);

        return builder.Build();
    }
}
