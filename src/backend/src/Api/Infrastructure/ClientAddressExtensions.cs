namespace Api.Infrastructure;

/// <summary>Where a request came from.</summary>
internal static class ClientAddressExtensions
{
    /// <summary>The client's address, or null when the connection does not say.</summary>
    /// <remarks>
    /// Correct only because forwarded headers ran first and trust only the
    /// configured proxies.
    /// </remarks>
    /// <param name="context">The request.</param>
    internal static string? ClientAddress(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Connection.RemoteIpAddress?.ToString();
    }
}
