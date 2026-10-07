using Dapper;
using Npgsql;

namespace Infrastructure.Persistence;

/// <summary>The single seam every repository runs SQL through.</summary>
/// <remarks>
/// Enlisting in the request's transaction, passing the cancellation token and taking and returning
/// the pooled connection happen here, not in every repository method.
/// </remarks>
internal sealed class DbExecutor(DbSession session)
{
    /// <summary>Reads at most one row.</summary>
    internal Task<TRow?> QuerySingleOrDefaultAsync<TRow>(
        string sql,
        object? parameters,
        CancellationToken cancellationToken) =>
        RunAsync(sql, parameters,
            (connection, command) => connection.QuerySingleOrDefaultAsync<TRow>(command),
            cancellationToken);

    /// <summary>Reads every matching row.</summary>
    internal Task<IReadOnlyList<TRow>> QueryAsync<TRow>(
        string sql,
        object? parameters,
        CancellationToken cancellationToken) =>
        RunAsync<IReadOnlyList<TRow>>(sql, parameters,
            async (connection, command) => [.. await connection.QueryAsync<TRow>(command).ConfigureAwait(false)],
            cancellationToken);

    /// <summary>Runs a statement and returns the number of rows it affected.</summary>
    /// <remarks>
    /// Zero rows from an update whose WHERE carries <c>version</c> is how a concurrency conflict is
    /// detected: the check lives in the SQL, never in C#.
    /// </remarks>
    internal Task<int> ExecuteAsync(
        string sql,
        object? parameters,
        CancellationToken cancellationToken) =>
        RunAsync(sql, parameters,
            (connection, command) => connection.ExecuteAsync(command),
            cancellationToken);

    /// <summary>Reads a single value.</summary>
    internal Task<TValue?> ExecuteScalarAsync<TValue>(
        string sql,
        object? parameters,
        CancellationToken cancellationToken) =>
        RunAsync(sql, parameters,
            (connection, command) => connection.ExecuteScalarAsync<TValue>(command),
            cancellationToken);

    /// <summary>
    /// Reads several result sets, for loading a whole aggregate in one round trip.
    /// </summary>
    /// <remarks>
    /// The sets are read inside <paramref name="read"/> because the connection can go back to the
    /// pool only once they have been.
    /// </remarks>
    internal Task<TResult> QueryMultipleAsync<TResult>(
        string sql,
        object? parameters,
        Func<SqlMapper.GridReader, Task<TResult>> read,
        CancellationToken cancellationToken) =>
        RunAsync(sql, parameters,
            async (connection, command) =>
            {
                var reader = await connection.QueryMultipleAsync(command).ConfigureAwait(false);

                await using (reader.ConfigureAwait(false))
                {
                    return await read(reader).ConfigureAwait(false);
                }
            },
            cancellationToken);

    private async Task<TOut> RunAsync<TOut>(
        string sql,
        object? parameters,
        Func<NpgsqlConnection, CommandDefinition, Task<TOut>> run,
        CancellationToken cancellationToken)
    {
        var connection = await session.ConnectionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var command = new CommandDefinition(
                sql,
                parameters,
                transaction: session.Transaction,
                cancellationToken: cancellationToken);

            return await run(connection, command).ConfigureAwait(false);
        }
        finally
        {
            // Outside a transaction this statement was all the connection was needed for.
            await session.ReleaseAsync().ConfigureAwait(false);
        }
    }
}
