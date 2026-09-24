using Application.Abstractions.Settings;
using Infrastructure.Persistence;
using Npgsql;

namespace IntegrationTests.Suggestions.Replay;

/// <summary>
/// A restored copy of a real Culina database, for replaying a real cook log.
/// </summary>
/// <remarks>
/// <para>
/// A copy and never the live one. The replay rolls back everything it does,
/// but it does it by deleting a household's history inside a transaction, and
/// the row locks that takes are nothing a running instance should wait on.
/// </para>
/// <para>
/// Restore a backup somewhere local, then run the explicit tests that read it
/// with <c>CULINA_REPLAY_DATABASE="Host=…;Database=…;Username=…;Password=…"</c>.
/// </para>
/// </remarks>
internal static class RestoredDatabase
{
    internal const string Variable = "CULINA_REPLAY_DATABASE";

    /// <summary>The connection string, or null when nobody set one.</summary>
    internal static string? ConnectionString => Environment.GetEnvironmentVariable(Variable);

    /// <summary>The app's own data source, so the replay reads the copy exactly as the app would.</summary>
    internal static NpgsqlDataSource Open(string connectionString)
    {
        var parts = new NpgsqlConnectionStringBuilder(connectionString);

        DapperConfiguration.Apply();

        return CulinaDataSource.Build(new DatabaseSettings
        {
            Host = parts.Host!,
            Port = parts.Port,
            Name = parts.Database!,
            Username = parts.Username!,
            Password = parts.Password ?? string.Empty,
            RequireSsl = parts.SslMode is SslMode.Require or SslMode.VerifyCA or SslMode.VerifyFull
        });
    }

    /// <summary>Every household that has cooked anything.</summary>
    internal static Task<IReadOnlyList<Guid>> HouseholdsAsync(DbSession session, CancellationToken cancellationToken) =>
        new DbExecutor(session).QueryAsync<Guid>(
            "select distinct household_id from cook_log_entries;",
            null,
            cancellationToken);
}
