namespace Application.Abstractions;

/// <summary>
/// Answers whether the database is reachable.
/// </summary>
/// <remarks>
/// A port rather than a direct call, because the readiness endpoint lives in
/// the presentation layer and must not know that PostgreSQL, Npgsql or SQL
/// exist. This is a real technology boundary, not an interface added for a
/// test.
/// </remarks>
public interface IDatabaseProbe
{
    /// <summary>Runs the cheapest possible round trip.</summary>
    /// <param name="cancellationToken">Cancels the probe.</param>
    Task<bool> IsReachableAsync(CancellationToken cancellationToken);
}
