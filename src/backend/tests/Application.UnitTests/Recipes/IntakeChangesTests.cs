using Application.Abstractions;
using Application.Recipes.Intake;
using Contracts.Recipes.Intake;
using Domain.Shared;

namespace Application.UnitTests.Recipes;

/// <summary>What the intake stream tells its reader: who hears of a change, how changes pile up, and that silence is a heartbeat.</summary>
public class IntakeChangesTests
{
    private static readonly TimeSpan Soon = TimeSpan.FromMilliseconds(50);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task NextAsync_ShouldReturnEachChangedJobOnce_HoweverOftenItChanged()
    {
        var changes = new IntakeChanges();
        var user = Guid.NewGuid();
        var job = Guid.NewGuid();
        using var subscription = changes.Subscribe(user);

        changes.Changed(user, job);
        changes.Changed(user, job);

        Assert.Equal([job], await subscription.NextAsync(TimeSpan.FromSeconds(5), Token));
        Assert.Empty(await subscription.NextAsync(Soon, Token));
    }

    [Fact]
    public async Task NextAsync_ShouldHearOnlyItsOwnersJobs()
    {
        var changes = new IntakeChanges();
        using var mine = changes.Subscribe(Guid.NewGuid());

        changes.Changed(Guid.NewGuid(), Guid.NewGuid());

        Assert.Empty(await mine.NextAsync(Soon, Token));
    }

    [Fact]
    public async Task NextAsync_ShouldStopHearing_OnceDisposed()
    {
        var changes = new IntakeChanges();
        var user = Guid.NewGuid();
        var subscription = changes.Subscribe(user);

        subscription.Dispose();
        changes.Changed(user, Guid.NewGuid());

        Assert.Empty(await subscription.NextAsync(Soon, Token));
    }

    [Fact]
    public async Task WatchAsync_ShouldSendASnapshot_ThenChangedJobs_ThenAHeartbeat()
    {
        var user = Guid.NewGuid();
        var changes = new IntakeChanges();
        var jobs = new FakeJobs(user);
        var watch = new IntakeWatch(jobs, changes);
        await using var events = watch.WatchAsync(user, Soon, Token).GetAsyncEnumerator(Token);

        Assert.True(await events.MoveNextAsync());
        Assert.True(events.Current.Snapshot);
        Assert.Single(events.Current.Jobs);

        jobs.Stage = "writing";
        changes.Changed(user, jobs.Id);

        // The change may have raced the first wait; heartbeats before it are fine.
        IntakeEvent update;
        do
        {
            Assert.True(await events.MoveNextAsync());
            update = events.Current;
        }
        while (update.Jobs.Count == 0);

        Assert.False(update.Snapshot);
        Assert.Equal("writing", Assert.Single(update.Jobs).Stage);

        Assert.True(await events.MoveNextAsync());
        Assert.False(events.Current.Snapshot);
        Assert.Empty(events.Current.Jobs);
    }

    private sealed class FakeJobs(Guid owner) : IRecipeIntakeJobs
    {
        public Guid Id { get; } = Guid.NewGuid();

        public string Stage { get; set; } = "queued";

        private IntakeJob Job => new() { Id = Id, HouseholdId = Guid.Empty, Stage = Stage, CreatedAt = DateTimeOffset.UnixEpoch };

        public Task<IReadOnlyList<IntakeJob>> ListAsync(Guid userId, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<IntakeJob>>(userId == owner ? [Job] : []);

        public Task<IntakeJob?> GetAsync(Guid id, Guid userId, CancellationToken token) =>
            Task.FromResult<IntakeJob?>(userId == owner && id == Id ? Job : null);

        public Task<Result<IntakeJob>> EnqueueAsync(Guid id, Guid userId, Guid householdId, IntakeMaterial material, CancellationToken token) => throw new NotSupportedException();
        public Task<IntakeMaterial?> MaterialAsync(Guid id, Guid userId, CancellationToken token) => throw new NotSupportedException();
        public Task<IntakePhoto?> PhotoAsync(Guid id, Guid userId, int index, CancellationToken token) => throw new NotSupportedException();
        public Task SourceAsync(Guid id, IntakeMaterial material, CancellationToken token) => throw new NotSupportedException();
        public Task ReviewAsync(Guid id, Guid userId, CancellationToken token) => throw new NotSupportedException();
        public Task<IntakeWork?> ClaimAsync(CancellationToken token) => throw new NotSupportedException();
        public Task ProgressAsync(Guid id, string stage, Contracts.Recipes.Drafts.Response? draft, CancellationToken token) => throw new NotSupportedException();
        public Task CompleteAsync(Guid id, Guid recipeId, CancellationToken token) => throw new NotSupportedException();
        public Task FailAsync(Guid id, string errorCode, CancellationToken token) => throw new NotSupportedException();
    }
}
