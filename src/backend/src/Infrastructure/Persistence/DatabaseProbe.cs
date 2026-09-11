using Application.Abstractions;
using Npgsql;

namespace Infrastructure.Persistence;

/// <summary>
/// The readiness probe: one round trip, no schema knowledge.
/// </summary>
/// <param name="executor">Runs the statement.</param>
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
            // An unreachable database is the answer this probe exists to give,
            // not a defect to propagate: a 503 from readiness is what tells an
            // orchestrator to stop sending traffic.
            return false;
        }
    }
}
