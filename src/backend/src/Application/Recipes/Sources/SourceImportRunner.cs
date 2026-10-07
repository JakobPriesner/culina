using System.Diagnostics;
using Application.Abstractions;
using Application.Telemetry;
using Contracts.Recipes.Sources;
using Domain.Import;
using Domain.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.Recipes.Sources;

/// <summary>
/// Does the work of one import, away from the request that asked for it.
/// </summary>
/// <remarks>
/// <para>
/// The request that starts an import is over in milliseconds; the import is
/// not. Holding a connection open for ten minutes of somebody else's server
/// meant the browser's deadline, the proxy's deadline and a closed laptop were
/// each enough to lose the work — which is why the client used to slice its own
/// selection into batches and drive them one at a time.
/// </para>
/// <para>
/// Here instead: the whole selection, fetched a few at a time in parallel,
/// recording each outcome the moment it lands so anybody watching sees it. One
/// scope per recipe, because a unit of work is a database connection and two
/// recipes may not share one.
/// </para>
/// </remarks>
/// <param name="scopes">Creates the scope each recipe is written in.</param>
/// <param name="time">The injected clock.</param>
/// <param name="logger">Records how a run went, and anything that ended it.</param>
public sealed class SourceImportRunner(
    IServiceScopeFactory scopes,
    TimeProvider time,
    ILogger<SourceImportRunner> logger)
{
    /// <summary>
    /// How many recipes are fetched at the same time.
    /// </summary>
    /// <remarks>
    /// The limit is somebody else's server, not this one. Each recipe is a
    /// round trip for the recipe and usually a second for its photo, against an
    /// instance that may be on the end of a domestic upload — asking it for
    /// four hundred at once would be indistinguishable from attacking it. Four
    /// is enough to make an import several times faster than doing them one at
    /// a time, and few enough to stay a polite guest.
    /// </remarks>
    private const int AtOnce = 4;

    /// <summary>Runs an import to its end, whatever happens on the way.</summary>
    /// <param name="run">The import to do.</param>
    /// <param name="cancellationToken">Stops the run when the host stops.</param>
    public async Task RunAsync(ImportRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);

        // Its own trace: the request that queued it ended long ago, and the
        // recipes it fetches would otherwise be spans belonging to nothing.
        // Not "Recipes.Import", which is the paste-a-link handler.
        using var activity = CulinaTelemetry.ActivitySource.StartActivity("Recipes.ImportRun");
        activity?.SetTag("culina.import_id", run.Id);

        var startedAt = time.GetTimestamp();

        ImportLogs.RunStarted(logger, run.Id, run.Total, run.SourceId, run.CookbookId);

        try
        {
            await ImportAllAsync(run, cancellationToken).ConfigureAwait(false);

            var elapsed = (long)time.GetElapsedTime(startedAt).TotalMilliseconds;
            var imported = run.Count(RecipeImporter.Imported);
            var failed = run.Count(RecipeImporter.Failed);

            ImportLogs.RunFinished(logger, run.Id, elapsed, imported, run.Total, failed);
        }
        catch (OperationCanceledException)
        {
            // The host is stopping. What arrived is written and has its origin
            // row, so asking for the same selection again brings over the rest.
            ImportLogs.RunStopped(logger, run.Id);
        }
#pragma warning disable CA1031 // A defect in one import must not take the worker down with it.
        catch (Exception failure)
#pragma warning restore CA1031
        {
            activity?.SetStatus(ActivityStatusCode.Error, failure.GetType().Name);
            ImportLogs.RunFailed(logger, run.Id, failure);
        }
        finally
        {
            // Always: a watcher waits for this event and nothing else ends it.
            run.Finish();
        }
    }

    private async Task ImportAllAsync(ImportRun run, CancellationToken cancellationToken)
    {
        var scope = scopes.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            var services = scope.ServiceProvider;

            // Looked up again in this scope rather than carried over from the
            // request's: an entity belongs to the connection it was read on.
            var found = await SourceAccess
                .ReadableAsync(
                    services.GetRequiredService<IRecipeSourceRepository>(),
                    services.GetRequiredService<IHouseholdRepository>(),
                    run.SourceId,
                    run.UserId,
                    cancellationToken)
                .ConfigureAwait(false);

            await found.Match(
                source => BringOverAsync(run, source, services, cancellationToken),
                error => RefuseAsync(run, error)).ConfigureAwait(false);
        }
    }

    private async Task BringOverAsync(
        ImportRun run,
        RecipeSource source,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var reader = services.GetRequiredService<IRecipeLibraries>().For(source.Kind);

        // Once for the run, not once per recipe: every recipe in one import
        // lands in the language of the one person who asked for it.
        var theirs = await services.GetRequiredService<IUserPreferencesRepository>()
            .GetAsync(run.UserId, cancellationToken)
            .ConfigureAwait(false);

        await reader.Match(
            async library =>
            {
                var alreadyHere = await services.GetRequiredService<IRecipeOriginRepository>()
                    .AlreadyHereAsync(source.HouseholdId, source.Kind, [.. run.ExternalIds], cancellationToken)
                    .ConfigureAwait(false);

                await EachAsync(
                        run,
                        new ImportInto(
                            source,
                            library,
                            run.CookbookId,
                            run.UserId,
                            theirs.Language ?? run.DeviceLanguage,
                            run.AllowLookalikes,
                            alreadyHere),
                        cancellationToken)
                    .ConfigureAwait(false);

                await MarkUsedAsync(source, services, cancellationToken).ConfigureAwait(false);
            },
            error => RefuseAsync(run, error)).ConfigureAwait(false);
    }

    /// <summary>
    /// Every recipe asked for, a few at a time, each in a scope of its own.
    /// </summary>
    /// <remarks>
    /// One task per recipe and one await for all of them, so the run is over
    /// when the last one is. The gate is what keeps "in parallel" from meaning
    /// "all at once" — without it, four hundred recipes would be four hundred
    /// simultaneous connections to their server and to this one's pool.
    /// </remarks>
    private async Task EachAsync(
        ImportRun run,
        ImportInto into,
        CancellationToken cancellationToken)
    {
        using var gate = new SemaphoreSlim(AtOnce, AtOnce);

        await Task.WhenAll(run.ExternalIds.Select(async externalId =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var outcome = await OneAsync(into, externalId, cancellationToken).ConfigureAwait(false);

                ImportLogs.RecipeDone(logger, run.Id, outcome.ExternalId, outcome.Outcome, outcome.Reason);
                run.Record(outcome);
            }
            finally
            {
                gate.Release();
            }
        })).ConfigureAwait(false);
    }

    private async Task<ImportedRecipe> OneAsync(
        ImportInto into,
        string externalId,
        CancellationToken cancellationToken)
    {
        var scope = scopes.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            try
            {
                return await scope.ServiceProvider
                    .GetRequiredService<RecipeImporter>()
                    .ImportAsync(into, externalId, cancellationToken)
                    .ConfigureAwait(false);
            }
#pragma warning disable CA1031 // One recipe that could not be written is one line in the result.
            catch (Exception failure) when (failure is not OperationCanceledException)
#pragma warning restore CA1031
            {
                ImportLogs.RecipeFailed(logger, externalId, failure);

                return Unreadable(externalId, ImportErrors.CouldNotImport);
            }
        }
    }

    /// <summary>
    /// Says the same thing about every recipe asked for.
    /// </summary>
    /// <remarks>
    /// The connection was disconnected, or this no longer knows how to read
    /// that app. The import cannot start at all — but it was accepted, so the
    /// answer is a reason per recipe rather than a silence.
    /// </remarks>
    private static Task RefuseAsync(ImportRun run, Error error)
    {
        foreach (var externalId in run.ExternalIds)
        {
            run.Record(Unreadable(externalId, error));
        }

        return Task.CompletedTask;
    }

    private static ImportedRecipe Unreadable(string externalId, Error error) => new()
    {
        ExternalId = externalId,
        Outcome = "failed",
        Reason = error.Code
    };

    private async Task MarkUsedAsync(
        RecipeSource source,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        source.Used(time.GetUtcNow());

        var sources = services.GetRequiredService<IRecipeSourceRepository>();

        await services.GetRequiredService<IUnitOfWork>().InTransactionAsync(
                async token =>
                {
                    await sources.SaveAsync(source, token).ConfigureAwait(false);

                    return true;
                },
                cancellationToken)
            .ConfigureAwait(false);
    }
}

/// <summary>
/// Import log lines. Event ids 1210-1219.
/// </summary>
internal static partial class ImportLogs
{
    [LoggerMessage(
        EventId = 1210,
        Level = LogLevel.Warning,
        Message = "Import {ImportId} stopped before it finished")]
    internal static partial void RunStopped(ILogger logger, Guid importId);

    [LoggerMessage(
        EventId = 1211,
        Level = LogLevel.Error,
        Message = "Import {ImportId} ended in a defect")]
    internal static partial void RunFailed(ILogger logger, Guid importId, Exception failure);

    [LoggerMessage(
        EventId = 1212,
        Level = LogLevel.Error,
        Message = "Recipe {ExternalId} could not be brought over")]
    internal static partial void RecipeFailed(ILogger logger, string externalId, Exception failure);

    /// <remarks>
    /// The rest of the total were already here or looked like a recipe that
    /// was, and were left alone.
    /// </remarks>
    [LoggerMessage(
        EventId = 1213,
        Level = LogLevel.Information,
        Message = "Import {ImportId} finished in {ElapsedMilliseconds} ms: {Imported} of {Total} brought over, {Failed} failed")]
    internal static partial void RunFinished(
        ILogger logger,
        Guid importId,
        long elapsedMilliseconds,
        int imported,
        int total,
        int failed);

    [LoggerMessage(
        EventId = 1214,
        Level = LogLevel.Debug,
        Message = "Import {ImportId} started: {Total} recipes from source {SourceId} onto cookbook {CookbookId}")]
    internal static partial void RunStarted(ILogger logger, Guid importId, int total, Guid sourceId, Guid cookbookId);

    [LoggerMessage(
        EventId = 1215,
        Level = LogLevel.Debug,
        Message = "Import {ImportId}: recipe {ExternalId} {Outcome} {Reason}")]
    internal static partial void RecipeDone(
        ILogger logger,
        Guid importId,
        string externalId,
        string outcome,
        string? reason);
}
