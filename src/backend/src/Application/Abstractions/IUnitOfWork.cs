using Domain.Shared;

namespace Application.Abstractions;

/// <summary>Runs several writes as one atomic step.</summary>
/// <remarks>
/// For writes that span statements (a recipe with its ingredients and steps); a single statement is
/// already atomic. A handler wraps its work, so a forgotten commit or a thrown exception cannot
/// leave a transaction open.
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// Runs <paramref name="work"/> in a transaction: commits on success, rolls back on a returned
    /// failure or exception.
    /// </summary>
    Task<Result> InTransactionAsync(
        Func<CancellationToken, Task<Result>> work,
        CancellationToken cancellationToken);

    /// <summary>
    /// Runs <paramref name="work"/> in a transaction: commits on success, rolls back on a returned
    /// failure or exception.
    /// </summary>
    Task<Result<TValue>> InTransactionAsync<TValue>(
        Func<CancellationToken, Task<Result<TValue>>> work,
        CancellationToken cancellationToken)
        where TValue : notnull;

    /// <summary>
    /// Runs <paramref name="work"/> in a transaction: commits on return, rolls back on an
    /// exception.
    /// </summary>
    /// <remarks>
    /// For work that fails only by throwing; <see cref="Result"/>-returning work binds to the
    /// overloads above.
    /// </remarks>
    Task<TResult> InTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        CancellationToken cancellationToken);
}
