namespace Application.Abstractions.Settings;

/// <summary>Which proxies the app trusts to tell it the real client address.</summary>
/// <remarks>
/// Without it every client looks like the proxy (per-IP limits and logs break); trusting every proxy would let any client forge its address.
/// </remarks>
public sealed record ForwardedHeadersSettings
{
    /// <summary>The configuration section these values are read from.</summary>
    public const string SectionName = "ForwardedHeaders";

    /// <summary>The proxy addresses whose <c>X-Forwarded-*</c> headers are honoured. Empty means no proxy.</summary>
    public IReadOnlyList<string> KnownProxies { get; init; } = [];

    /// <summary>Proxy <em>networks</em> (CIDR) whose headers are honoured; a container proxy's address is not known in advance.</summary>
    /// <remarks>
    /// A trust boundary, so keep it small: nothing wider than a <c>/8</c> (IPv4) or <c>/32</c> (IPv6) without <see cref="DangerouslyTrustWideNetworks"/>.
    /// </remarks>
    public IReadOnlyList<string> KnownNetworks { get; init; } = [];

    /// <summary>Accepts a <see cref="KnownNetworks"/> entry wider than a <c>/8</c> (IPv4) or <c>/32</c> (IPv6).</summary>
    /// <remarks>
    /// Never offered on the settings screen: such a network lets every client in it claim any address, voiding rate limits and the security log.
    /// A container network needs no override (<c>172.16.0.0/12</c> at widest).
    /// </remarks>
    public bool DangerouslyTrustWideNetworks { get; init; }

    private const int NarrowestIPv4Prefix = 8;
    private const int NarrowestIPv6Prefix = 32;

    /// <summary>The first of <see cref="KnownNetworks"/> too wide to trust, or null when none is or trusting one was asked for by name.</summary>
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
