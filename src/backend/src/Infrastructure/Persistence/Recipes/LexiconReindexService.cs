using System.Diagnostics;
using Application.Telemetry;
using Domain.Search;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence.Recipes;

/// <summary>Brings every search document up to the lexicon this build ships with.</summary>
/// <remarks>
/// Raise <see cref="CulinaryLexicon.Version"/> and ship: stale rows are rebuilt in batches at startup, before requests
/// are taken. Runs after the migrations (hosted services start in order). Each row is its own statement outside a
/// transaction, so a restart resumes where it stopped.
/// </remarks>
internal sealed class LexiconReindexService(
    IServiceScopeFactory scopeFactory,
    ILogger<LexiconReindexService> logger) : IHostedService
{
    private const int BatchSize = 200;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var activity = CulinaTelemetry.ActivitySource.StartActivity("Search.Reindex");

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

            activity?.SetTag("culina.reindexed", total);

            if (total > 0)
            {
                logger.Reindexed(total, CulinaryLexicon.Version, clock.ElapsedMilliseconds);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Search index log lines. Event ids 1940-1949.</summary>
internal static partial class SearchIndexLogs
{
    [LoggerMessage(
        EventId = 1940,
        Level = LogLevel.Information,
        Message = "Rebuilt the concepts of {Count} search documents for lexicon {Version} in {ElapsedMilliseconds} ms")]
    internal static partial void Reindexed(this ILogger logger, int count, int version, long elapsedMilliseconds);
}
