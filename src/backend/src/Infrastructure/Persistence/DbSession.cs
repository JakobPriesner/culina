using Npgsql;

namespace Infrastructure.Persistence;

/// <summary>
/// The database connection a request is using, and the transaction it is
/// currently enlisted in.
/// </summary>
/// <remarks>
/// <para>
/// Scoped, opened lazily, and handed back to the pool as soon as nothing needs
/// it: after each statement outside a transaction, and when a transaction
/// ends. A request that streams for minutes after reading its session — an
/// import being watched, a draft being written — then holds no connection
/// while it waits, so the pool has to be as large as the statements running
/// at once rather than the requests open at once.
/// </para>
/// <para>
/// Repositories ask for a connection and stay unaware of transactions. Whether
/// their statements are atomic is decided by the handler through
/// <see cref="UnitOfWork"/>, which is the only thing that opens or closes one.
/// </para>
/// </remarks>
/// <param name="dataSource">The pooled data source.</param>
/// <param name="watch">Warns when getting a connection is slow, or one is held too long.</param>
internal sealed class DbSession(NpgsqlDataSource dataSource, ConnectionPoolWatch watch) : IAsyncDisposable
{
    private NpgsqlConnection? connection;
    private long acquiredAt;
    private bool pinned;

    internal NpgsqlTransaction? Transaction { get; private set; }

    internal async ValueTask<NpgsqlConnection> ConnectionAsync(CancellationToken cancellationToken)
    {
        if (connection is null)
        {
            var requestedAt = watch.Timestamp();

            connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            acquiredAt = watch.Acquired(requestedAt);
        }

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

        await ReleaseAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Keeps the connection out of the pool until the returned scope ends, for
    /// state that lives on the connection rather than in a transaction — a
    /// session-level advisory lock. Released, the connection is reset before
    /// anyone uses it again, and that state goes with it.
    /// </summary>
    internal async ValueTask<IAsyncDisposable> PinAsync(CancellationToken cancellationToken)
    {
        await ConnectionAsync(cancellationToken).ConfigureAwait(false);

        pinned = true;

        return new Pin(this);
    }

    /// <summary>Hands the connection back to the pool, unless a transaction or a pin still needs it.</summary>
    internal async ValueTask ReleaseAsync()
    {
        if (connection is null || Transaction is not null || pinned)
        {
            return;
        }

        await connection.DisposeAsync().ConfigureAwait(false);
        connection = null;

        watch.Returned(acquiredAt);
    }

    public async ValueTask DisposeAsync()
    {
        pinned = false;

        // Ending a transaction releases its connection; without one, there may
        // still be a connection to release.
        await EndTransactionAsync().ConfigureAwait(false);
        await ReleaseAsync().ConfigureAwait(false);
    }

    private ValueTask UnpinAsync()
    {
        pinned = false;

        // Held on purpose, for as long as its owner needed, so not reported as
        // held too long.
        acquiredAt = watch.Timestamp();

        return ReleaseAsync();
    }

    private sealed class Pin(DbSession session) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => session.UnpinAsync();
    }
}
