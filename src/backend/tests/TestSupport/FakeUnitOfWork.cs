using Application.Abstractions;

namespace TestSupport;

/// <summary>
/// Runs the work without a database.
/// </summary>
/// <remarks>
/// Records whether a handler asked for a transaction, which is a behaviour
/// worth asserting for a multi-statement write, and lets a test make the
/// transaction fail to check the handler's own error path.
/// </remarks>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    /// <summary>How many times a handler wrapped work in a transaction.</summary>
    public int Transactions { get; private set; }

    /// <summary>When set, the transaction throws instead of running the work.</summary>
    public Exception? FailWith { get; set; }

    public Task<TResult> InTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        Transactions++;

        return FailWith is null ? work(cancellationToken) : Task.FromException<TResult>(FailWith);
    }
}
