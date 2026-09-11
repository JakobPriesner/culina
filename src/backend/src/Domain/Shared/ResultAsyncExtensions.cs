namespace Domain.Shared;

/// <summary>
/// Composition across an <c>await</c>, so a chain does not have to be broken
/// into intermediate variables every time a step is asynchronous.
/// </summary>
/// <remarks>
/// There is no <c>Result&lt;Task&lt;T&gt;&gt;</c>: an asynchronous operation
/// returns <c>Task&lt;Result&lt;T&gt;&gt;</c>, and these extensions chain from
/// that.
/// </remarks>
public static class ResultAsyncExtensions
{
    /// <summary>Transforms a successful value once the task completes.</summary>
    /// <typeparam name="TIn">The incoming value.</typeparam>
    /// <typeparam name="TOut">The outgoing value.</typeparam>
    /// <param name="task">The pending result.</param>
    /// <param name="map">Runs only on success.</param>
    public static async Task<Result<TOut>> MapAsync<TIn, TOut>(
        this Task<Result<TIn>> task,
        Func<TIn, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(task);

        var result = await task.ConfigureAwait(false);

        return result.Map(map);
    }

    /// <summary>Chains an asynchronous operation that can itself fail.</summary>
    /// <typeparam name="TIn">The incoming value.</typeparam>
    /// <typeparam name="TOut">The outgoing value.</typeparam>
    /// <param name="task">The pending result.</param>
    /// <param name="bind">Runs only on success.</param>
    public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
        this Task<Result<TIn>> task,
        Func<TIn, Task<Result<TOut>>> bind)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(bind);

        var result = await task.ConfigureAwait(false);

        return await result
            .Match(bind, error => Task.FromResult(Result<TOut>.Failure(error)))
            .ConfigureAwait(false);
    }

    /// <summary>Guards a successful value once the task completes.</summary>
    /// <typeparam name="TValue">The carried value.</typeparam>
    /// <param name="task">The pending result.</param>
    /// <param name="predicate">The condition the value must satisfy.</param>
    /// <param name="error">The failure to return when it does not.</param>
    public static async Task<Result<TValue>> EnsureAsync<TValue>(
        this Task<Result<TValue>> task,
        Func<TValue, bool> predicate,
        Error error)
    {
        ArgumentNullException.ThrowIfNull(task);

        var result = await task.ConfigureAwait(false);

        return result.Ensure(predicate, error);
    }
}
