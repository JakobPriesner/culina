using System.Diagnostics;
using Domain.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence.Recipes;

/// <summary>
/// Brings every search document up to the lexicon this build ships with.
/// </summary>
/// <remarks>
/// <para>
/// How a lexicon change is deployed: raise <see cref="CulinaryLexicon.Version"/>
/// and ship the container. At startup the rows another version built are
/// rebuilt here, in batches, before the host starts taking requests — so the
/// first search after an upgrade searches a complete index, which is the same
/// promise migration 0012 kept by backfilling inside itself.
/// </para>
/// <para>
/// Registered after the migrations, which run first because hosted services
/// start in the order they were added: the column this writes has to exist.
/// </para>
/// <para>
/// Each row is its own statement outside any transaction. A restart halfway
/// through leaves some rows done and the rest still stale, and the next start
/// carries on from there; nothing is ever half-written.
/// </para>
/// </remarks>
/// <param name="scopeFactory">Makes the scope the writer and its connection live in.</param>
/// <param name="logger">Says how much was rebuilt.</param>
internal sealed class LexiconReindexService(
    IServiceScopeFactory scopeFactory,
    ILogger<LexiconReindexService> logger) : IHostedService
{
    private const int BatchSize = 200;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var scope = scopeFactory.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            var writer = scope.ServiceProvider.GetRequiredService<SearchDocumentWriter>();
            var clock = Stopwatch.StartNew();
            var total = 0;
            int done;

            do
            {
                done = await writer.ReindexStaleAsync(BatchSize, cancellationToken).ConfigureAwait(false);
                total += done;
            }
            while (done == BatchSize);

            if (total > 0)
            {
                logger.Reindexed(total, CulinaryLexicon.Version, clock.ElapsedMilliseconds);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Search index log lines. Event ids 1940-1949.
/// </summary>
internal static partial class SearchIndexLogs
{
    [LoggerMessage(
        EventId = 1940,
        Level = LogLevel.Information,
        Message = "Rebuilt the concepts of {Count} search documents for lexicon {Version} in {ElapsedMilliseconds} ms")]
    internal static partial void Reindexed(this ILogger logger, int count, int version, long elapsedMilliseconds);
}
