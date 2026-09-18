using Application.Recipes.Sources;
using Contracts.Recipes.Sources;

namespace Application.UnitTests.Recipes;

/// <summary>
/// What an import tells the people watching it.
/// </summary>
/// <remarks>
/// The property under test throughout is that watching is a <em>read</em>, not
/// a subscription: the run happens whether or not anybody is connected, and a
/// watcher that arrives late, or comes back after dropping, is told exactly
/// what it missed and nothing it already had. That is what lets a person close
/// a laptop in the middle of four hundred recipes.
/// </remarks>
public class ImportRunTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WatchAsync_ShouldReplayWhatAlreadyHappened_ForAWatcherThatArrivedLate()
    {
        // Arrange
        var run = Run("a", "b");
        run.Record(Imported("a"));
        run.Record(Imported("b"));
        run.Finish();

        // Act
        var events = await WatchAsync(run, from: 0);

        // Assert
        // Two recipes and the last event: nothing about the run depended on
        // somebody being connected while it happened.
        Assert.Equal(["a", "b", null], events.Select(one => one.Recipe?.ExternalId));
        Assert.Equal([1, 2, 2], events.Select(one => one.Done));
        Assert.Equal([false, false, true], events.Select(one => one.Finished));
    }

    [Fact]
    public async Task WatchAsync_ShouldSkipWhatTheCallerHas_WhenItReconnects()
    {
        // Arrange
        var run = Run("a", "b", "c");
        run.Record(Imported("a"));
        run.Record(Imported("b"));
        run.Record(Imported("c"));
        run.Finish();

        // Act
        // Two already seen, which is what the event ids on them said.
        var events = await WatchAsync(run, from: 2);

        // Assert
        // A dropped connection loses nothing and repeats nothing — the count
        // still counts the two it did not resend.
        Assert.Equal(["c", null], events.Select(one => one.Recipe?.ExternalId));
        Assert.Equal([3, 3], events.Select(one => one.Done));
    }

    [Fact]
    public async Task WatchAsync_ShouldReportEachOutcome_AsItLands()
    {
        // Arrange
        var run = Run("a", "b");

        // Act
        var watching = WatchAsync(run, from: 0);

        run.Record(Imported("a"));
        run.Record(Imported("b"));
        run.Finish();

        var events = await watching;

        // Assert
        // The point of the stream: a recipe is reported when it is done, not
        // when the run is.
        Assert.Equal(["a", "b", null], events.Select(one => one.Recipe?.ExternalId));
        Assert.True(events[^1].Finished);
    }

    [Fact]
    public async Task WatchAsync_ShouldEnd_WhenTheRunDidNothingAtAll()
    {
        // Arrange
        // A connection that was disconnected, say: every recipe failed and the
        // run is over.
        var run = Run("a");
        run.Finish();

        // Act
        var events = await WatchAsync(run, from: 0);

        // Assert
        var only = Assert.Single(events);
        Assert.True(only.Finished);
        Assert.Null(only.Recipe);
    }

    [Fact]
    public void Start_ShouldQueueTheRun_AndMakeItFindable()
    {
        // Arrange
        var runs = new ImportRuns(TimeProvider.System);
        var run = Run("a");

        // Act
        runs.Start(run);

        // Assert
        // Findable is what a watcher needs; queued is what the worker needs.
        Assert.Same(run, runs.Find(run.Id));
        Assert.Null(runs.Find(Guid.NewGuid()));
    }

    [Fact]
    public async Task QueuedAsync_ShouldHandOutRuns_InTheOrderTheyWereAskedFor()
    {
        // Arrange
        var runs = new ImportRuns(TimeProvider.System);
        var first = Run("a");
        var second = Run("b");

        // Act
        runs.Start(first);
        runs.Start(second);

        List<ImportRun> queued = [];

        await foreach (var run in runs.QueuedAsync(Token))
        {
            queued.Add(run);

            if (queued.Count == 2)
            {
                break;
            }
        }

        // Assert
        Assert.Equal([first.Id, second.Id], queued.Select(one => one.Id));
    }

    private static async Task<List<ImportEvent>> WatchAsync(ImportRun run, int from)
    {
        List<ImportEvent> events = [];

        await foreach (var one in run.WatchAsync(from, Token))
        {
            events.Add(one);
        }

        return events;
    }

    private static ImportRun Run(params string[] externalIds) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "From Tandoor, 17 September",
        externalIds,
        DateTimeOffset.UnixEpoch);

    private static ImportedRecipe Imported(string externalId) => new()
    {
        ExternalId = externalId,
        Outcome = "imported",
        RecipeId = Guid.NewGuid()
    };
}
