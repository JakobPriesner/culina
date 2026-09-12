namespace Application.Abstractions.Settings;

/// <summary>
/// Which proxies the app trusts to tell it the real client address.
/// </summary>
/// <remarks>
/// This matters more than it looks: without it the app sees the reverse proxy's
/// address as every client's, so per-IP rate limiting protects nothing and
/// security logs name the wrong host. Trusting <em>every</em> proxy would be
/// worse — any client could then forge its own address.
/// </remarks>
public sealed record ForwardedHeadersSettings
{
    /// <summary>The configuration section these values are read from.</summary>
    public const string SectionName = "ForwardedHeaders";

    /// <summary>
    /// The proxy addresses whose <c>X-Forwarded-*</c> headers are honoured.
    /// Empty means the app is not behind a proxy and reads the socket address
    /// directly.
    /// </summary>
    public IReadOnlyList<string> KnownProxies { get; init; } = [];

    /// <summary>
    /// Proxy <em>networks</em>, in CIDR form, whose headers are honoured.
    /// </summary>
    /// <remarks>
    /// Not a convenience. A reverse proxy in a container network has no address
    /// anyone can know in advance — it is whatever the bridge hands out this
    /// time — so a deployment that can only name addresses cannot name its own
    /// proxy at all. Keep the network as small as it can be: this is a trust
    /// boundary, and <c>0.0.0.0/0</c> means every client may forge its own
    /// address.
    /// </remarks>
    public IReadOnlyList<string> KnownNetworks { get; init; } = [];

    /// <summary>Throws when any value would make the process unable to serve.</summary>
    public void Validate()
    {
        foreach (var proxy in KnownProxies)
        {
            if (!System.Net.IPAddress.TryParse(proxy, out _))
            {
                throw new InvalidOperationException(
                    $"Configuration {SectionName}__KnownProxies contains '{proxy}', "
                    + "which is not an IP address. List proxy addresses, comma-separated — "
                    + $"or, for a network, use {SectionName}__KnownNetworks with a CIDR range.");
            }
        }

        foreach (var network in KnownNetworks)
        {
            if (!System.Net.IPNetwork.TryParse(network, out _))
            {
                throw new InvalidOperationException(
                    $"Configuration {SectionName}__KnownNetworks contains '{network}', "
                    + "which is not a CIDR range such as 172.18.0.0/16.");
            }
        }
    }
}
