namespace Domain.Shared;

/// <summary>
/// Composition over results: each step runs only if the previous one succeeded; all written in
/// terms of <c>Match</c>.
/// </summary>
public static class ResultExtensions
{
    /// <summary>Transforms a successful value, leaving a failure untouched.</summary>
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> map)
        where TIn : notnull
        where TOut : notnull
    {
        ArgumentNullException.ThrowIfNull(map);

        return result.Match(value => Result<TOut>.Success(map(value)), Result<TOut>.Failure);
    }

    /// <summary>Produces a value from a valueless success.</summary>
    public static Result<TOut> Map<TOut>(this Result result, Func<TOut> map)
        where TOut : notnull
    {
        ArgumentNullException.ThrowIfNull(map);

        return result.Match(() => Result<TOut>.Success(map()), Result<TOut>.Failure);
    }

    /// <summary>Chains an operation that can itself fail.</summary>
    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> bind)
        where TIn : notnull
        where TOut : notnull
    {
        ArgumentNullException.ThrowIfNull(bind);

        return result.Match(bind, Result<TOut>.Failure);
    }

    /// <summary>Chains an operation that can fail and yields no value.</summary>
    public static Result Bind<TIn>(this Result<TIn> result, Func<TIn, Result> bind)
        where TIn : notnull
    {
        ArgumentNullException.ThrowIfNull(bind);

        return result.Match(bind, Result.Failure);
    }

    /// <summary>
    /// Chains a value-producing operation onto a valueless success, so a guard can precede the
    /// work.
    /// </summary>
    public static Result<TOut> Bind<TOut>(this Result result, Func<Result<TOut>> bind)
        where TOut : notnull
    {
        ArgumentNullException.ThrowIfNull(bind);

        return result.Match(bind, Result<TOut>.Failure);
    }

    /// <summary>Chains a valueless operation onto a valueless success.</summary>
    public static Result Bind(this Result result, Func<Result> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);

        return result.Match(bind, Result.Failure);
    }

    /// <summary>Runs a side effect on success and returns the result unchanged.</summary>
    public static Result<TValue> Tap<TValue>(this Result<TValue> result, Action<TValue> onSuccess)
        where TValue : notnull
    {
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.Bind(value =>
        {
            onSuccess(value);
            return Result<TValue>.Success(value);
        });
    }

    /// <summary>Runs a side effect on a valueless success and returns it unchanged.</summary>
    public static Result Tap(this Result result, Action onSuccess)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);

        return result.Bind(() =>
        {
            onSuccess();
            return Result.Success();
        });
    }

    /// <summary>Gathers many results into one, failing on the first failure.</summary>
    /// <remarks>
    /// Unlike <see cref="Result.Combine"/> it stops at the first failure: later values are usually
    /// parsed from earlier ones.
    /// </remarks>
    public static Result<IReadOnlyList<TValue>> Collect<TValue>(
        this IEnumerable<Result<TValue>> results)
        where TValue : notnull
    {
        ArgumentNullException.ThrowIfNull(results);

        List<TValue> values = [];

        foreach (var result in results)
        {
            var failure = result.Match(
                value =>
                {
                    values.Add(value);

                    return (Error?)null;
                },
                error => error);

            if (failure is not null)
            {
                return Result<IReadOnlyList<TValue>>.Failure(failure);
            }
        }

        return values;
    }
}
