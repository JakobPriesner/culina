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
/// How many outcomes the caller already has, so a reconnect resumes; zero reads from the beginning.
/// </param>
public sealed record WatchImportQuery(Guid SourceId, Guid ImportId, Guid UserId, int From);

/// <summary>An import, and its outcomes as they land.</summary>
/// <param name="ImportId">Which import.</param>
/// <param name="Total">How many recipes it is bringing over.</param>
/// <param name="CookbookId">The shelf they are landing on.</param>
/// <param name="CookbookName">What that shelf is called.</param>
/// <param name="Events">
/// One event per recipe finished, then one saying the run is over; replays history before waiting.
/// </param>
public sealed record ImportProgress(
    Guid ImportId,
    int Total,
    Guid CookbookId,
    string CookbookName,
    IAsyncEnumerable<ImportEvent> Events);

/// <summary>
/// Hands a caller the events of a run they started; a query, since watching changes nothing.
/// </summary>
internal sealed class WatchImportQueryHandler(ImportRuns runs)
    : IQueryHandler<WatchImportQuery, ImportProgress>
{
    public Task<Result<ImportProgress>> Handle(
        WatchImportQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var run = runs.Find(query.ImportId);

        // Not the owner and a forgotten run are both "no such import", without saying which.
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
