using System.Globalization;
using Application.Abstractions.Settings;
using Infrastructure.Persistence.Recipes;
using Npgsql;

namespace Infrastructure.Persistence;

/// <summary>Builds the pooled data source from the configured connection parts.</summary>
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
            // SCRAM only: a server asking for clear text or MD5 (as an impostor would) is refused before anything is sent.
            RequireAuth = "ScramSHA256",
            // Npgsql tries GSS first, which loads libgssapi_krb5, absent from the chiseled image, aborting the process on first connect.
            GssEncryptionMode = GssEncryptionMode.Disable,
            MaxPoolSize = settings.MaxPoolSize,
            // So a DBA can tell in pg_stat_activity which process holds a connection.
            ApplicationName = "culina-api",
            // Fail fast behind an unreachable host; the health check and retry need to know quickly.
            Timeout = 10,
            CommandTimeout = 30,
            // The `<<%` operator reads its threshold from this setting and cannot be given one, so it is sent at connect time
            // from the constant the search uses; as a startup option it survives the pooled-connection reset.
            Options = "-c pg_trgm.strict_word_similarity_threshold="
                + RecipeSearcher.FuzzyThreshold.ToString(CultureInfo.InvariantCulture)
        }.ConnectionString;

        var builder = new NpgsqlDataSourceBuilder(connectionString);

        return builder.Build();
    }
}
