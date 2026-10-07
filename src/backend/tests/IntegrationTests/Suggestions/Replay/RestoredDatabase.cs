using Application.Abstractions.Settings;
using Infrastructure.Persistence;
using Npgsql;

namespace IntegrationTests.Suggestions.Replay;

/// <summary>A restored copy of a real Culina database for replaying a real cook log.</summary>
/// <remarks>
/// Never the live one: the replay rolls back by deleting a household's history in a transaction, and its
/// row locks must not block a running instance. Enable with <c>CULINA_REPLAY_DATABASE="Host=…;Database=…;Username=…;Password=…"</c>.
/// </remarks>
internal static class RestoredDatabase
{
    internal const string Variable = "CULINA_REPLAY_DATABASE";

    internal static string? ConnectionString => Environment.GetEnvironmentVariable(Variable);

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

    internal static Task<IReadOnlyList<Guid>> HouseholdsAsync(DbSession session, CancellationToken cancellationToken) =>
        new DbExecutor(session).QueryAsync<Guid>(
            "select distinct household_id from cook_log_entries;",
            null,
            cancellationToken);
}
