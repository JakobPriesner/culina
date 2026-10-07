using Npgsql;

namespace Infrastructure.Persistence;

/// <summary>The database connection a request is using, and the transaction it is currently enlisted in.</summary>
/// <remarks>
/// Scoped and opened lazily, and returned to the pool as soon as nothing needs it, so long-streaming requests
/// hold no connection while waiting. Repositories are unaware of transactions; <see cref="UnitOfWork"/> alone opens and closes one.
/// </remarks>
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

    /// <summary>Keeps the connection out of the pool until the scope ends, for connection-level state such as a session advisory lock.</summary>
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

        // Ending a transaction releases its connection; without one there may still be one to release.
        await EndTransactionAsync().ConfigureAwait(false);
        await ReleaseAsync().ConfigureAwait(false);
    }

    private ValueTask UnpinAsync()
    {
        pinned = false;

        // Held on purpose, so not reported as held too long.
        acquiredAt = watch.Timestamp();

        return ReleaseAsync();
    }

    private sealed class Pin(DbSession session) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => session.UnpinAsync();
    }
}
