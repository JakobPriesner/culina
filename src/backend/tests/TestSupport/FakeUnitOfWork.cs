using Application.Abstractions;
using Domain.Shared;

namespace TestSupport;

/// <summary>
/// Runs the work without a database, recording whether a handler asked for a transaction and
/// letting a test make it fail.
/// </summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    /// <summary>How many times a handler wrapped work in a transaction.</summary>
    public int Transactions { get; private set; }

    /// <summary>When set, the transaction throws instead of running the work.</summary>
    public Exception? FailWith { get; set; }

    public Task<Result> InTransactionAsync(
        Func<CancellationToken, Task<Result>> work,
        CancellationToken cancellationToken) =>
        InTransactionAsync<Result>(work, cancellationToken);

    public Task<Result<TValue>> InTransactionAsync<TValue>(
        Func<CancellationToken, Task<Result<TValue>>> work,
        CancellationToken cancellationToken)
        where TValue : notnull =>
        InTransactionAsync<Result<TValue>>(work, cancellationToken);

    public Task<TResult> InTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        Transactions++;

        return FailWith is null ? work(cancellationToken) : Task.FromException<TResult>(FailWith);
    }
}
