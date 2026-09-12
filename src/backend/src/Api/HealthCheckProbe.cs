using System.Globalization;

namespace Api;

/// <summary>
/// Asks the running container whether it is ready, and exits saying so.
/// </summary>
/// <remarks>
/// <para>
/// Docker's <c>HEALTHCHECK</c> runs a command inside the container, and the
/// image that ships has no shell and no curl — that is the point of a chiseled
/// runtime. So the app answers the question about itself: the same binary,
/// invoked with a flag, makes one request to its own port and exits 0 or 1.
/// </para>
/// <para>
/// It asks <c>/health/ready</c> rather than <c>/health/live</c> on purpose.
/// "A process exists" is not the question a load balancer is asking; "can this
/// container serve a request that needs the database" is.
/// </para>
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
            // Refused, reset or too slow. All of them mean "not ready", and
            // none of them is worth a stack trace in the health log.
            return 1;
        }
    }

    /// <summary>
    /// The port the app is actually listening on.
    /// </summary>
    /// <remarks>
    /// Read from the same configuration the host reads, so a deployment that
    /// changes the port does not silently leave the health check probing the
    /// old one — which would report a healthy container as unhealthy forever.
    /// </remarks>
    private static int Port(IConfiguration configuration)
    {
        var ports = configuration["ASPNETCORE_HTTP_PORTS"];
        var first = ports?.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

        return int.TryParse(first, CultureInfo.InvariantCulture, out var port) ? port : 8080;
    }
}
