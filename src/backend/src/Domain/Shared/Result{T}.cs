namespace Domain.Shared;

/// <summary>
/// The outcome of an operation that either produced a <typeparamref name="TValue"/> or failed with
/// an <see cref="Error"/>.
/// </summary>
/// <remarks>
/// <see cref="Match{TOut}"/> is the only way to observe it. <typeparamref name="TValue"/> is
/// non-nullable, so "absent" cannot be a null inside a success.
/// </remarks>
public readonly struct Result<TValue>
    where TValue : notnull
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
    public static Result<TValue> Success(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new Result<TValue>(value, error: null);
    }

    /// <summary>A failed outcome.</summary>
    public static Result<TValue> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Result<TValue>(value: default, error);
    }

    /// <summary>Lets a handler return the value directly.</summary>
    public static implicit operator Result<TValue>(TValue value) => Success(value);

    /// <summary>Lets a handler return an error directly.</summary>
    public static implicit operator Result<TValue>(Error error) => Failure(error);

    /// <summary>Observes the outcome, producing a value from whichever branch ran.</summary>
    public TOut Match<TOut>(Func<TValue, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        ResultGuard.AgainstUninitialised(observable);

        return error is null ? onSuccess(value!) : onFailure(error);
    }

    /// <summary>Observes the outcome without producing a value.</summary>
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
