using Application.Abstractions;
using Domain.Sessions;
using Domain.Shared;
using Infrastructure.Identity;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntegrationTests.Identity;

/// <summary>Housekeeping must not take the host down: an unhandled exception in a background service stops it.</summary>
public sealed class ExpiredSessionSweeperTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly TickingTime time = new();
    private readonly RecordingLogs logs = new();
    private readonly ILoggerFactory loggers;
    private readonly FailingOnceStore store = new();
    private readonly ServiceProvider services;
    private readonly ExpiredSessionSweeper sweeper;

    public ExpiredSessionSweeperTests()
    {
        loggers = LoggerFactory.Create(builder => builder.AddProvider(logs));
        services = new ServiceCollection().AddScoped<ISessionStore>(_ => store).BuildServiceProvider();
        sweeper = new ExpiredSessionSweeper(
            services.GetRequiredService<IServiceScopeFactory>(),
            time,
            loggers.CreateLogger<ExpiredSessionSweeper>());
    }

    [Fact]
    public async Task Sweeper_ShouldLogAndKeepRunning_WhenASweepThrows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await sweeper.StartAsync(cancellationToken);
        await store.WaitForSweepAsync(1, Patience, cancellationToken);

        var line = await logs.WaitForAsync(line => line.Level == LogLevel.Error);
        Assert.NotNull(line);
        Assert.Equal(1921, line.EventId);
        Assert.Contains("database is restarting", line.Exception, StringComparison.Ordinal);
        Assert.False(sweeper.ExecuteTask!.IsCompleted);

        await sweeper.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task Sweeper_ShouldSweepAgain_OnTheNextTick()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await sweeper.StartAsync(cancellationToken);
        await store.WaitForSweepAsync(1, Patience, cancellationToken);

        time.Elapse();
        await store.WaitForSweepAsync(2, Patience, cancellationToken);

        Assert.Equal(2, store.Sweeps);

        await sweeper.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task Sweeper_ShouldStopPromptly_WhenTheHostShutsDownBetweenSweeps()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await sweeper.StartAsync(cancellationToken);
        await store.WaitForSweepAsync(1, Patience, cancellationToken);

        await sweeper.StopAsync(cancellationToken).WaitAsync(Patience, cancellationToken);

        Assert.True(sweeper.ExecuteTask!.IsCompleted);
    }

    public void Dispose()
    {
        sweeper.Dispose();
        services.Dispose();
        loggers.Dispose();
        logs.Dispose();
    }

    /// <summary>Throws on the first sweep, as Postgres restarting would, then succeeds.</summary>
    private sealed class FailingOnceStore : ISessionStore
    {
        private int sweeps;

        public int Sweeps => sweeps;

        public async Task WaitForSweepAsync(int count, TimeSpan patience, CancellationToken cancellationToken)
        {
            using var deadline = new CancellationTokenSource(patience);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);

            while (Volatile.Read(ref sweeps) < count)
            {
                await Task.Delay(10, linked.Token);
            }
        }

        public Task<int> DeleteExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
        {
            var count = Interlocked.Increment(ref sweeps);

            return count == 1
                ? throw new InvalidOperationException("database is restarting")
                : Task.FromResult(0);
        }

        public Task<Result<Session>> FindActiveByTokenAsync(string token, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Session>> ForUserAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result> AddAsync(Session session, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result> RenewAsync(Session session, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result> RevokeAsync(Guid sessionId, Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RevokeAllAsync(Guid userId, Guid? keepSessionId, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
