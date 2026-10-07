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
            // Signs in by SCRAM only, which never hands the server the
            // password or anything it could replay. A server asking for it in
            // clear text or as MD5 — what one that only pretends to be
            // PostgreSQL asks, to be sent it — is refused before anything is
            // sent. PostgreSQL has defaulted to SCRAM since version 14.
            RequireAuth = "ScramSHA256",
            // Npgsql tries GSS encryption first unless told not to, which loads
            // libgssapi_krb5 — absent from the chiseled image, so the process
            // aborts on the first connection. Culina never uses Kerberos.
            GssEncryptionMode = GssEncryptionMode.Disable,
            MaxPoolSize = settings.MaxPoolSize,
            // Named so a DBA looking at pg_stat_activity can tell which process
            // holds a connection.
            ApplicationName = "culina-api",
            // Fail fast rather than hang a request behind an unreachable host;
            // the health check and the retry both need to know quickly.
            Timeout = 10,
            CommandTimeout = 30,
            // What the trigram index on titles hands back for the typo lane.
            // The `<<%` operator reads its threshold from this setting and
            // cannot be given one, so it is sent at connect time from the
            // constant the search compares against; as a startup option it also
            // survives the reset a pooled connection gets between requests.
            Options = "-c pg_trgm.strict_word_similarity_threshold="
                + RecipeSearcher.FuzzyThreshold.ToString(CultureInfo.InvariantCulture)
        }.ConnectionString;

        var builder = new NpgsqlDataSourceBuilder(connectionString);

        return builder.Build();
    }
}
