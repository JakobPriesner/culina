namespace IntegrationTests.Fixtures;

/// <summary>A clock whose timers fire only when a test says so, to drive a <see cref="PeriodicTimer"/> tick by tick.</summary>
public sealed class TickingTime : TimeProvider
{
    private Action? elapse;

    /// <summary>Elapses the timer the service created, as if its period had elapsed.</summary>
    public void Elapse() => (elapse ?? throw new InvalidOperationException("No timer was created."))();

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        new Tick(callback, state, action => elapse = action);

    private sealed class Tick : ITimer
    {
        internal Tick(TimerCallback callback, object? state, Action<Action> register) =>
            register(() => callback(state));

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
