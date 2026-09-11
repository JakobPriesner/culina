using Application.Abstractions;

namespace Infrastructure.Persistence;

/// <summary>
/// Runs a handler's writes inside one transaction.
/// </summary>
/// <param name="session">The request's connection and transaction.</param>
internal sealed class UnitOfWork(DbSession session) : IUnitOfWork
{
    public async Task<TResult> InTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);

        var transaction = await session.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var result = await work(cancellationToken).ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return result;
        }
        finally
        {
            // Disposing an uncommitted transaction rolls it back, so there is
            // no catch here to swallow the original failure and no path that
            // can leave a transaction open.
            await session.EndTransactionAsync().ConfigureAwait(false);
        }
    }
}
