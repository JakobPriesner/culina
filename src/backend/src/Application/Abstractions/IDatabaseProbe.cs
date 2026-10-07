namespace Application.Abstractions;

/// <summary>Answers whether the database is reachable, keeping PostgreSQL out of the presentation layer.</summary>
public interface IDatabaseProbe
{
    /// <summary>Runs the cheapest possible round trip.</summary>
    /// <param name="cancellationToken">Cancels the probe.</param>
    Task<bool> IsReachableAsync(CancellationToken cancellationToken);
}
