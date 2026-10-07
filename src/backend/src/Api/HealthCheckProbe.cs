using System.Globalization;

namespace Api;

/// <summary>Asks the running container whether it is ready, and exits saying so.</summary>
/// <remarks>
/// The chiseled image has no shell or curl for Docker's <c>HEALTHCHECK</c>, so the same binary,
/// given a flag, requests its own port and exits 0 or 1. It asks <c>/health/ready</c>, not live: a
/// load balancer needs "can this serve a request that needs the database".
/// </remarks>
internal static class HealthCheckProbe
{
    internal const string Flag = "--health-check";

    /// <summary>Long enough for a loaded container, short enough to be a check.</summary>
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    internal static bool Requested(string[] args) =>
        args.Contains(Flag, StringComparer.Ordinal);

    /// <summary>Zero when the container is ready to serve, one when it is not.</summary>
    internal static async Task<int> RunAsync(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        using var client = new HttpClient { Timeout = Deadline };

        try
        {
            var response = await client
                .GetAsync(new Uri($"http://127.0.0.1:{Port(configuration)}/health/ready"))
                .ConfigureAwait(false);

            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            // Refused, reset or too slow all mean "not ready" and deserve no stack trace.
            return 1;
        }
    }

    /// <summary>
    /// The port the app is actually listening on, read from the host's configuration so a changed
    /// port cannot leave the check probing the old one.
    /// </summary>
    private static int Port(IConfiguration configuration)
    {
        var ports = configuration["ASPNETCORE_HTTP_PORTS"];
        var first = ports?.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

        return int.TryParse(first, CultureInfo.InvariantCulture, out var port) ? port : 8080;
    }
}
