using System.Globalization;
using Application.Abstractions.Settings;
using Infrastructure.Persistence.Recipes;
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
            CommandTimeout = 30,
            // What the trigram index on titles hands back for the typo lane.
            // The `<%` operator reads its threshold from this setting and cannot
            // be given one, so it is sent at connect time from the constant the
            // search compares against; as a startup option it also survives the
            // reset a pooled connection gets between requests.
            Options = "-c pg_trgm.word_similarity_threshold="
                + RecipeSearcher.FuzzyThreshold.ToString(CultureInfo.InvariantCulture)
        }.ConnectionString;

        var builder = new NpgsqlDataSourceBuilder(connectionString);

        return builder.Build();
    }
}
