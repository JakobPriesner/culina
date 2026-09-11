using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Persistence.Migrations;

/// <summary>
/// Brings the schema up to date before the app accepts traffic.
/// </summary>
/// <remarks>
/// <para>
/// Runs as a hosted service so it completes during startup. A failure is fatal:
/// a half-migrated database must not serve requests, and the process exits
/// non-zero so an orchestrator does not route traffic to it.
/// </para>
/// <para>
/// The runner and its session are scoped, so this opens its own scope and
/// disposes it — releasing the advisory lock with the connection.
/// </para>
/// </remarks>
/// <param name="scopeFactory">Creates the scope the runner lives in.</param>
internal sealed class MigrationHostedService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var scope = scopeFactory.CreateAsyncScope();

        await using (scope.ConfigureAwait(false))
        {
            var runner = scope.ServiceProvider.GetRequiredService<MigrationRunner>();

            await runner.ApplyAsync(EmbeddedMigrations.Load(), cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
