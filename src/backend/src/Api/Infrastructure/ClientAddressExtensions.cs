namespace Api.Infrastructure;

/// <summary>Where a request came from.</summary>
internal static class ClientAddressExtensions
{
    /// <summary>The client's address, or null when the connection does not say; correct because forwarded headers ran first.</summary>
    internal static string? ClientAddress(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Connection.RemoteIpAddress?.ToString();
    }
}
