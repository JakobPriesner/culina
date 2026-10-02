using Domain.Shared;

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
    /// succeeds and rolling back when it returns a failure or throws.
    /// </summary>
    /// <param name="work">The writes to perform.</param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    Task<Result> InTransactionAsync(
        Func<CancellationToken, Task<Result>> work,
        CancellationToken cancellationToken);

    /// <summary>
    /// Runs <paramref name="work"/> inside a transaction, committing when it
    /// succeeds and rolling back when it returns a failure or throws.
    /// </summary>
    /// <typeparam name="TValue">What a successful outcome carries.</typeparam>
    /// <param name="work">The writes to perform.</param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    Task<Result<TValue>> InTransactionAsync<TValue>(
        Func<CancellationToken, Task<Result<TValue>>> work,
        CancellationToken cancellationToken)
        where TValue : notnull;

    /// <summary>
    /// Runs <paramref name="work"/> inside a transaction, committing when it
    /// returns and rolling back if it throws.
    /// </summary>
    /// <remarks>
    /// For work that cannot fail except by throwing. Work that returns a
    /// <see cref="Result"/> or <see cref="Result{TValue}"/> binds to the
    /// overloads above, which also roll back on a returned failure.
    /// </remarks>
    /// <typeparam name="TResult">What the work produces.</typeparam>
    /// <param name="work">The writes to perform.</param>
    /// <param name="cancellationToken">Cancels the work and rolls back.</param>
    Task<TResult> InTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken);
}
