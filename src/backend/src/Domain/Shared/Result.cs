namespace Domain.Shared;

/// <summary>
/// The outcome of an operation that succeeded or failed with an <see cref="Error"/>, with no value on success.
/// Expected failures are returned, defects thrown; <see cref="Match{TOut}"/> is the only way to observe it.
/// </summary>
public readonly struct Result
{
    private readonly Error? error;
    private readonly bool observable;

    private Result(Error? error)
    {
        this.error = error;
        observable = true;
    }

    /// <summary>A successful outcome.</summary>
    public static Result Success() => new(error: null);

    /// <summary>A failed outcome.</summary>
    /// <param name="error">The failure. Must not be null.</param>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Result(error);
    }

    /// <summary>Lets a handler <c>return UserErrors.NotFound(id);</c> without naming <see cref="Failure"/>.</summary>
    /// <param name="error">The failure to wrap.</param>
    public static implicit operator Result(Error error) => Failure(error);

    /// <summary>Runs every check and reports all failures at once.</summary>
    /// <param name="results">The checks to combine.</param>
    /// <returns>
    /// Success when every check passed; the single failure when exactly one
    /// failed; otherwise a <see cref="ValidationError"/> over all of them.
    /// </returns>
    public static Result Combine(params IReadOnlyList<Result> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        List<Error> failures = [];

        foreach (var result in results)
        {
            result.Match(() => { }, failures.Add);
        }

        return failures.Count == 0 ? Success() : Failure(ErrorAggregate.Of(failures));
    }

    /// <summary>Observes the outcome, producing a value from whichever branch ran.</summary>
    /// <typeparam name="TOut">What both branches produce.</typeparam>
    /// <param name="onSuccess">Runs when the operation succeeded.</param>
    /// <param name="onFailure">Runs when the operation failed.</param>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        ResultGuard.AgainstUninitialised(observable);

        return error is null ? onSuccess() : onFailure(error);
    }

    /// <summary>Observes the outcome without producing a value.</summary>
    /// <param name="onSuccess">Runs when the operation succeeded.</param>
    /// <param name="onFailure">Runs when the operation failed.</param>
    public void Match(Action onSuccess, Action<Error> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        ResultGuard.AgainstUninitialised(observable);

        if (error is null)
        {
            onSuccess();
        }
        else
        {
            onFailure(error);
        }
    }
}
