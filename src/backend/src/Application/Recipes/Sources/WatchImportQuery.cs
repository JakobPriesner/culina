using Application.Abstractions.Messaging;
using Contracts.Recipes.Sources;
using Domain.Import;
using Domain.Shared;

namespace Application.Recipes.Sources;

/// <summary>Follows an import that is already running.</summary>
/// <param name="SourceId">Which connection it is reading.</param>
/// <param name="ImportId">Which import.</param>
/// <param name="UserId">Who is asking.</param>
/// <param name="From">
/// How many outcomes the caller already has, so a reconnect picks up where it
/// stopped. Zero reads the run from the beginning, which is what makes a stream
/// that was never connected and one that dropped the same thing.
/// </param>
public sealed record WatchImportQuery(Guid SourceId, Guid ImportId, Guid UserId, int From);

/// <summary>An import, and its outcomes as they land.</summary>
/// <param name="ImportId">Which import.</param>
/// <param name="Total">How many recipes it is bringing over.</param>
/// <param name="CookbookId">The shelf they are landing on.</param>
/// <param name="CookbookName">What that shelf is called.</param>
/// <param name="Events">
/// One event per recipe finished, then one saying the run is over. It replays
/// what already happened before it waits for anything new.
/// </param>
public sealed record ImportProgress(
    Guid ImportId,
    int Total,
    Guid CookbookId,
    string CookbookName,
    IAsyncEnumerable<ImportEvent> Events);

/// <summary>
/// Hands a caller the events of a run they started.
/// </summary>
/// <remarks>
/// A query rather than a command: watching an import changes nothing about it,
/// and an import with nobody watching runs exactly the same.
/// </remarks>
internal sealed class WatchImportQueryHandler(ImportRuns runs)
    : IQueryHandler<WatchImportQuery, ImportProgress>
{
    public Task<Result<ImportProgress>> Handle(
        WatchImportQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var run = runs.Find(query.ImportId);

        // Whoever asked for the import is who may watch it — and a run that has
        // been forgotten, or that belongs to somebody else, is equally "no such
        // import". Nothing here says which of the two it was.
        if (run is null || run.SourceId != query.SourceId || run.UserId != query.UserId)
        {
            return Task.FromResult(
                Result<ImportProgress>.Failure(ImportErrors.ImportNotFound));
        }

        return Task.FromResult(Result<ImportProgress>.Success(new ImportProgress(
            run.Id,
            run.Total,
            run.CookbookId,
            run.CookbookName,
            run.WatchAsync(query.From, cancellationToken))));
    }
}
