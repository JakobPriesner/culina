namespace Domain.Shared;

/// <summary>
/// The outcome of an operation that either succeeded or failed with an
/// <see cref="Error"/>, and which carries no value on success.
/// </summary>
/// <remarks>
/// <para>
/// Expected failure is a <c>Result</c>; a defect is an exception. "That recipe
/// does not exist" is an ordinary outcome of a working system and is returned,
/// never thrown.
/// </para>
/// <para>
/// <see cref="Match{TOut}"/> is the only way to observe the outcome. There is no
/// public <c>IsSuccess</c> or <c>Error</c> to branch on, which makes it
/// impossible to read an error that is not there.
/// </para>
/// </remarks>
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

    /// <summary>
    /// Lets a handler write <c>return UserErrors.NotFound(id);</c> without
    /// naming <see cref="Failure"/>.
    /// </summary>
    /// <param name="error">The failure to wrap.</param>
    public static implicit operator Result(Error error) => Failure(error);

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
