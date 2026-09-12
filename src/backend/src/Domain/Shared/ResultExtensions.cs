namespace Domain.Shared;

/// <summary>
/// Composition over results: each step runs only if the previous one succeeded.
/// </summary>
/// <remarks>
/// Everything here is written in terms of <c>Match</c>, which is why
/// <see cref="Result"/> needs to expose no internal accessors at all.
/// </remarks>
public static class ResultExtensions
{
    /// <summary>Transforms a successful value, leaving a failure untouched.</summary>
    /// <typeparam name="TIn">The incoming value.</typeparam>
    /// <typeparam name="TOut">The outgoing value.</typeparam>
    /// <param name="result">The result to transform.</param>
    /// <param name="map">Runs only on success.</param>
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        return result.Match(value => Result<TOut>.Success(map(value)), Result<TOut>.Failure);
    }

    /// <summary>Produces a value from a valueless success.</summary>
    /// <typeparam name="TOut">The outgoing value.</typeparam>
    /// <param name="result">The result to transform.</param>
    /// <param name="map">Runs only on success.</param>
    public static Result<TOut> Map<TOut>(this Result result, Func<TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);

        return result.Match(() => Result<TOut>.Success(map()), Result<TOut>.Failure);
    }

    /// <summary>Chains an operation that can itself fail.</summary>
    /// <typeparam name="TIn">The incoming value.</typeparam>
    /// <typeparam name="TOut">The outgoing value.</typeparam>
    /// <param name="result">The result to chain from.</param>
    /// <param name="bind">Runs only on success.</param>
    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);

        return result.Match(bind, Result<TOut>.Failure);
    }

    /// <summary>Chains an operation that can fail and yields no value.</summary>
    /// <typeparam name="TIn">The incoming value.</typeparam>
    /// <param name="result">The result to chain from.</param>
    /// <param name="bind">Runs only on success.</param>
    public static Result Bind<TIn>(this Result<TIn> result, Func<TIn, Result> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);

        return result.Match(bind, Result.Failure);
    }

    /// <summary>
    /// Chains a value-producing operation onto a valueless success, so a guard
    /// can precede the work that produces something.
    /// </summary>
    /// <typeparam name="TOut">The outgoing value.</typeparam>
    /// <param name="result">The guard's outcome.</param>
    /// <param name="bind">Runs only when the guard passed.</param>
    public static Result<TOut> Bind<TOut>(this Result result, Func<Result<TOut>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);

        return result.Match(bind, Result<TOut>.Failure);
    }

    /// <summary>Chains a valueless operation onto a valueless success.</summary>
    /// <param name="result">The result to chain from.</param>
    /// <param name="bind">Runs only on success.</param>
    public static Result Bind(this Result result, Func<Result> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);

        return result.Match(bind, Result.Failure);
    }

    /// <summary>Fails a success whose value does not satisfy <paramref name="predicate"/>.</summary>
    /// <typeparam name="TValue">The carried value.</typeparam>
    /// <param name="result">The result to guard.</param>
    /// <param name="predicate">The condition the value must satisfy.</param>
    /// <param name="error">The failure to return when it does not.</param>
    public static Result<TValue> Ensure<TValue>(
        this Result<TValue> result,
        Func<TValue, bool> predicate,
        Error error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);

        return result.Bind(value =>
            predicate(value) ? Result<TValue>.Success(value) : Result<TValue>.Failure(error));
    }

    /// <summary>Runs a side effect on success and returns the result unchanged.</summary>
    /// <typeparam name="TValue">The carried value.</typeparam>
    /// <param name="result">The result to observe.</param>
    /// <param name="onSuccess">The side effect.</param>
    public static Result<TValue> Tap<TValue>(this Result<TValue> result, Action<TValue> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.Bind(value =>
        {
            onSuccess(value);
            return Result<TValue>.Success(value);
        });
    }

    /// <summary>Runs a side effect on a valueless success and returns it unchanged.</summary>
    /// <param name="result">The result to observe.</param>
    /// <param name="onSuccess">The side effect.</param>
    public static Result Tap(this Result result, Action onSuccess)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.Bind(() =>
        {
            onSuccess();
            return Result.Success();
        });
    }

    /// <summary>Turns a possibly-absent reference into a result.</summary>
    /// <typeparam name="TValue">The carried value.</typeparam>
    /// <param name="value">The value, or null when it was not found.</param>
    /// <param name="error">The failure to report when it is absent.</param>
    public static Result<TValue> ToResult<TValue>(this TValue? value, Error error)
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(error);

        return value is null ? Result<TValue>.Failure(error) : Result<TValue>.Success(value);
    }

    /// <summary>Turns a possibly-absent value type into a result.</summary>
    /// <typeparam name="TValue">The carried value.</typeparam>
    /// <param name="value">The value, or null when it was not found.</param>
    /// <param name="error">The failure to report when it is absent.</param>
    public static Result<TValue> ToResult<TValue>(this TValue? value, Error error)
        where TValue : struct
    {
        ArgumentNullException.ThrowIfNull(error);

        return value.HasValue ? Result<TValue>.Success(value.Value) : Result<TValue>.Failure(error);
    }
}
