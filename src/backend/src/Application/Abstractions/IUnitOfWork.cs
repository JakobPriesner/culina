namespace Application.Abstractions;

/// <summary>
/// Runs several writes as one atomic step.
/// </summary>
/// <remarks>
/// <para>
/// Most commands are a single statement and need nothing from this: PostgreSQL
/// already makes one statement atomic. It exists for the writes that genuinely
/// span statements — saving a recipe with its ingredients and steps, merging a
/// recipe into a shopping list — where a partial write would leave the data
/// inconsistent.
/// </para>
/// <para>
/// A handler wraps its work rather than calling begin and commit itself, so a
/// forgotten commit is not possible and a thrown exception cannot leave a
/// transaction open.
/// </para>
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// Runs <paramref name="work"/> inside a transaction, committing when it
    /// returns and rolling back if it throws.
    /// </summary>
    /// <typeparam name="TResult">What the work produces.</typeparam>
    /// <param name="work">The writes to perform.</param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    Task<TResult> InTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken);
}
