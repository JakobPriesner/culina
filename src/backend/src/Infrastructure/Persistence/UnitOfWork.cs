using Application.Abstractions;
using Domain.Shared;

namespace Infrastructure.Persistence;

/// <summary>Runs a handler's writes inside one transaction.</summary>
/// <param name="session">The request's connection and transaction.</param>
internal sealed class UnitOfWork(DbSession session) : IUnitOfWork
{
    public Task<Result> InTransactionAsync(
        Func<CancellationToken, Task<Result>> work,
        CancellationToken cancellationToken) =>
        RunAsync(work, result => result.Match(() => true, _ => false), cancellationToken);

    public Task<Result<TValue>> InTransactionAsync<TValue>(
        Func<CancellationToken, Task<Result<TValue>>> work,
        CancellationToken cancellationToken)
        where TValue : notnull =>
        RunAsync(work, result => result.Match(_ => true, _ => false), cancellationToken);

    public Task<TResult> InTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken) =>
        RunAsync(work, _ => true, cancellationToken);

    private async Task<TResult> RunAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        Func<TResult, bool> succeeded,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        var transaction = await session.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var result = await work(cancellationToken).ConfigureAwait(false);

            if (succeeded(result))
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
        finally
        {
            // Disposing an uncommitted transaction rolls it back, so a failed result or exception leaves nothing behind and no path leaves one open.
            await session.EndTransactionAsync().ConfigureAwait(false);
        }
    }
}
