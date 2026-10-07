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
    /// address. So nothing wider than a <c>/8</c> (IPv4) or a <c>/32</c>
    /// (IPv6) is accepted without <see cref="DangerouslyTrustWideNetworks"/>.
    /// </remarks>
    public IReadOnlyList<string> KnownNetworks { get; init; } = [];

    /// <summary>
    /// Accepts a <see cref="KnownNetworks"/> entry wider than a <c>/8</c>
    /// (IPv4) or a <c>/32</c> (IPv6).
    /// </summary>
    /// <remarks>
    /// Named to be noticed, and never offered on the settings screen. A network
    /// that wide is almost never a proxy's own; it is "make it work", and it
    /// lets every client inside it claim any address, which turns the
    /// per-address rate limits and the security log into whatever a client
    /// says. A container network is a <c>/12</c> at its widest
    /// (<c>172.16.0.0/12</c>) and needs no override.
    /// </remarks>
    public bool DangerouslyTrustWideNetworks { get; init; }

    private const int NarrowestIPv4Prefix = 8;
    private const int NarrowestIPv6Prefix = 32;

    /// <summary>
    /// The first of <see cref="KnownNetworks"/> too wide to trust, or null when
    /// none is — or when trusting one was asked for by name.
    /// </summary>
    public string? TooWideNetwork() =>
        DangerouslyTrustWideNetworks ? null : KnownNetworks.FirstOrDefault(IsTooWide);

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

        if (TooWideNetwork() is { } wide)
        {
            throw new InvalidOperationException(
                $"Configuration {SectionName}__KnownNetworks contains '{wide}', which would let every "
                + "client in it claim any address it likes. Name your proxy's own network, at most a "
                + $"/{NarrowestIPv4Prefix} for IPv4 or a /{NarrowestIPv6Prefix} for IPv6, such as 172.16.0.0/12 "
                + $"— or, if you truly mean it, set {SectionName}__{nameof(DangerouslyTrustWideNetworks)}=true.");
        }
    }

    /// <summary>Wider than a /8 for IPv4, or a /32 for IPv6.</summary>
    private static bool IsTooWide(string network) =>
        System.Net.IPNetwork.TryParse(network, out var parsed)
        && parsed.PrefixLength < (parsed.BaseAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            ? NarrowestIPv4Prefix
            : NarrowestIPv6Prefix);
}
