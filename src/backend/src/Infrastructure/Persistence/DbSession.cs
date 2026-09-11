using Npgsql;

namespace Infrastructure.Persistence;

/// <summary>
/// The one database connection a request uses, and the transaction it is
/// currently enlisted in.
/// </summary>
/// <remarks>
/// <para>
/// Scoped, and opened lazily: a request that reads nothing from the database —
/// a 401, a 304, a static file — never takes a connection from the pool.
/// </para>
/// <para>
/// Repositories ask for a connection and stay unaware of transactions. Whether
/// their statements are atomic is decided by the handler through
/// <see cref="UnitOfWork"/>, which is the only thing that opens or closes one.
/// </para>
/// </remarks>
/// <param name="dataSource">The pooled data source.</param>
internal sealed class DbSession(NpgsqlDataSource dataSource) : IAsyncDisposable
{
    private NpgsqlConnection? connection;

    internal NpgsqlTransaction? Transaction { get; private set; }

    internal async ValueTask<NpgsqlConnection> ConnectionAsync(CancellationToken cancellationToken)
    {
        connection ??= await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        return connection;
    }

    internal async ValueTask<NpgsqlTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (Transaction is not null)
        {
            throw new InvalidOperationException(
                "A transaction is already open for this request. Nested units of work are not "
                + "supported: wrap the outermost write instead.");
        }

        var open = await ConnectionAsync(cancellationToken).ConfigureAwait(false);

        Transaction = await open.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        return Transaction;
    }

    internal async ValueTask EndTransactionAsync()
    {
        if (Transaction is null)
        {
            return;
        }

        await Transaction.DisposeAsync().ConfigureAwait(false);
        Transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        await EndTransactionAsync().ConfigureAwait(false);

        if (connection is not null)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            connection = null;
        }
    }
}
