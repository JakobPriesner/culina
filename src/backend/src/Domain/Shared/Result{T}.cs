namespace Domain.Shared;

/// <summary>
/// The outcome of an operation that either produced a <typeparamref name="TValue"/>
/// or failed with an <see cref="Error"/>.
/// </summary>
/// <typeparam name="TValue">What a successful outcome carries.</typeparam>
/// <remarks>
/// <see cref="Match{TOut}"/> is the only way to observe the outcome, so the value
/// is reachable exactly when it exists.
/// </remarks>
public readonly struct Result<TValue>
{
    private readonly TValue? value;
    private readonly Error? error;
    private readonly bool observable;

    private Result(TValue? value, Error? error)
    {
        this.value = value;
        this.error = error;
        observable = true;
    }

    /// <summary>A successful outcome carrying <paramref name="value"/>.</summary>
    /// <param name="value">The produced value. Must not be null.</param>
    public static Result<TValue> Success(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new Result<TValue>(value, error: null);
    }

    /// <summary>A failed outcome.</summary>
    /// <param name="error">The failure. Must not be null.</param>
    public static Result<TValue> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Result<TValue>(value: default, error);
    }

    /// <summary>Lets a handler return the value directly.</summary>
    /// <param name="value">The produced value.</param>
    public static implicit operator Result<TValue>(TValue value) => Success(value);

    /// <summary>Lets a handler return an error directly.</summary>
    /// <param name="error">The failure to wrap.</param>
    public static implicit operator Result<TValue>(Error error) => Failure(error);

    /// <summary>Observes the outcome, producing a value from whichever branch ran.</summary>
    /// <typeparam name="TOut">What both branches produce.</typeparam>
    /// <param name="onSuccess">Runs with the value when the operation succeeded.</param>
    /// <param name="onFailure">Runs with the error when the operation failed.</param>
    public TOut Match<TOut>(Func<TValue, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        ResultGuard.AgainstUninitialised(observable);

        return error is null ? onSuccess(value!) : onFailure(error);
    }

    /// <summary>Observes the outcome without producing a value.</summary>
    /// <param name="onSuccess">Runs with the value when the operation succeeded.</param>
    /// <param name="onFailure">Runs with the error when the operation failed.</param>
    public void Match(Action<TValue> onSuccess, Action<Error> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        ResultGuard.AgainstUninitialised(observable);

        if (error is null)
        {
            onSuccess(value!);
        }
        else
        {
            onFailure(error);
        }
    }
}
