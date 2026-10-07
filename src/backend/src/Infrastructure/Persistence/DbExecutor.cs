using Dapper;
using Npgsql;

namespace Infrastructure.Persistence;

/// <summary>
/// The single seam every repository runs SQL through.
/// </summary>
/// <remarks>
/// It exists so that enlisting in the request's transaction, passing the
/// cancellation token, and taking the connection from the pool and giving it
/// back happen in one place rather than being repeated — and forgotten once —
/// in every repository method.
/// Repositories supply SQL and parameters; nothing else.
/// </remarks>
/// <param name="session">The request's connection and transaction.</param>
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
    /// Zero rows from an update whose WHERE carries <c>version</c> is how a
    /// concurrency conflict is detected — the check lives in the SQL, never in
    /// C#.
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
    /// Reads several result sets, for loading a whole aggregate in one round
    /// trip instead of one query per child collection.
    /// </summary>
    /// <remarks>
    /// The sets are read in <paramref name="read"/> rather than handed back,
    /// because they are read from the connection: it can go back to the pool
    /// only once they have been.
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
            // Outside a transaction, this statement was all the connection was
            // needed for.
            await session.ReleaseAsync().ConfigureAwait(false);
        }
    }
}
