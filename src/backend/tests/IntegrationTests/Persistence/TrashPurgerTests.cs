using Application.Abstractions.Messaging;
using Application.Trash.Purge;
using Domain.Shared;
using Infrastructure.Persistence.Trash;
using IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntegrationTests.Persistence;

/// <summary>Housekeeping must not take the host down: an unhandled exception in a background service stops it.</summary>
public sealed class TrashPurgerTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly TickingTime time = new();
    private readonly RecordingLogs logs = new();
    private readonly ILoggerFactory loggers;
    private readonly FailingOnceHandler handler = new();
    private readonly ServiceProvider services;
    private readonly TrashPurger purger;

    public TrashPurgerTests()
    {
        loggers = LoggerFactory.Create(builder => builder.AddProvider(logs));
        services = new ServiceCollection()
            .AddScoped<ICommandHandler<PurgeTrashCommand, int>>(_ => handler)
            .BuildServiceProvider();
        purger = new TrashPurger(
            services.GetRequiredService<IServiceScopeFactory>(),
            time,
            loggers.CreateLogger<TrashPurger>());
    }

    [Fact]
    public async Task Purger_ShouldLogAndKeepRunning_WhenAPurgeThrows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await purger.StartAsync(cancellationToken);
        await handler.WaitForPurgeAsync(1, Patience, cancellationToken);

        var line = await logs.WaitForAsync(line => line.Level == LogLevel.Error);
        Assert.NotNull(line);
        Assert.Equal(1952, line.EventId);
        Assert.Contains("database is restarting", line.Exception, StringComparison.Ordinal);
        Assert.False(purger.ExecuteTask!.IsCompleted);

        await purger.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task Purger_ShouldPurgeAgain_OnTheNextTick()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await purger.StartAsync(cancellationToken);
        await handler.WaitForPurgeAsync(1, Patience, cancellationToken);

        time.Elapse();
        await handler.WaitForPurgeAsync(2, Patience, cancellationToken);

        Assert.Equal(2, handler.Purges);

        await purger.StopAsync(cancellationToken);
    }

    [Fact]
    public async Task Purger_ShouldStopPromptly_WhenTheHostShutsDownBetweenPurges()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await purger.StartAsync(cancellationToken);
        await handler.WaitForPurgeAsync(1, Patience, cancellationToken);

        await purger.StopAsync(cancellationToken).WaitAsync(Patience, cancellationToken);

        Assert.True(purger.ExecuteTask!.IsCompleted);
    }

    public void Dispose()
    {
        purger.Dispose();
        services.Dispose();
        loggers.Dispose();
        logs.Dispose();
    }

    /// <summary>Throws on the first purge, as Postgres restarting would, then succeeds.</summary>
    private sealed class FailingOnceHandler : ICommandHandler<PurgeTrashCommand, int>
    {
        private int purges;

        public int Purges => purges;

        public async Task WaitForPurgeAsync(int count, TimeSpan patience, CancellationToken cancellationToken)
        {
            using var deadline = new CancellationTokenSource(patience);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);

            while (Volatile.Read(ref purges) < count)
            {
                await Task.Delay(10, linked.Token);
            }
        }

        public Task<Result<int>> Handle(PurgeTrashCommand command, CancellationToken cancellationToken)
        {
            var count = Interlocked.Increment(ref purges);

            return count == 1
                ? throw new InvalidOperationException("database is restarting")
                : Task.FromResult(Result<int>.Success(0));
        }
    }
}
