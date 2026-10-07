using Application.Abstractions;
using Npgsql;

namespace Infrastructure.Persistence;

/// <summary>The readiness probe: one round trip, no schema knowledge.</summary>
internal sealed class DatabaseProbe(DbExecutor executor) : IDatabaseProbe
{
    public async Task<bool> IsReachableAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await executor
                .ExecuteScalarAsync<int>("select 1;", null, cancellationToken)
                .ConfigureAwait(false) == 1;
        }
        catch (NpgsqlException)
        {
            // An unreachable database is the answer this probe exists to give; a 503 tells the orchestrator to stop sending traffic.
            return false;
        }
    }
}
