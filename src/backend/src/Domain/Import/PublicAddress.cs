using System.Net;
using System.Net.Sockets;

namespace Domain.Import;

/// <summary>Whether an address is one the server may be asked to fetch.</summary>
/// <remarks>
/// The server does the reading, so without this anyone could aim it at cloud metadata or port-scan the
/// private network. A deny list of non-public ranges applied to the <em>resolved address</em> every time,
/// including after redirects, since a name can resolve differently on the next lookup.
/// </remarks>
public static class PublicAddress
{
    // Cloud metadata inside private ranges (Alibaba, AWS over IPv6). Compared as bytes: IPAddress.Equals also
    // compares the IPv6 scope id.
    private static readonly byte[][] CloudMetadata =
    [
        [100, 100, 100, 200],
        IPAddress.Parse("fd00:ec2::254").GetAddressBytes()
    ];

    /// <summary>Whether the server may open a connection to this address.</summary>
    public static bool IsPublic(IPAddress? address)
    {
        if (address is null)
        {
            return false;
        }

        // An IPv4-mapped IPv6 address is the same address; checking the wrapper lets ::ffff:127.0.0.1 through.
        if (address.IsIPv4MappedToIPv6)
        {
            return IsPublic(address.MapToIPv4());
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPublicV4(address),
            AddressFamily.InterNetworkV6 => IsPublicV6(address),
            // Anything else is a unix socket or stranger.
            _ => false
        };
    }

    /// <summary>Whether an address is on a private network an operator may opt into.</summary>
    /// <remarks>
    /// Only RFC 1918, shared space (100.64.0.0/10) and IPv6 unique local: where a self-hosted recipe server
    /// lives. Loopback, link-local (cloud metadata), 0.0.0.0, multicast and broadcast are never allowed.
    /// </remarks>
    public static bool IsPrivateNetwork(IPAddress? address)
    {
        if (address is null)
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return IsPrivateNetwork(address.MapToIPv4());
        }

        var bytes = address.GetAddressBytes();

        if (Array.Exists(CloudMetadata, known => known.AsSpan().SequenceEqual(bytes)))
        {
            return false;
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => bytes[0] switch
            {
                10 => true,
                100 => bytes[1] is >= 64 and <= 127,
                172 => bytes[1] is >= 16 and <= 31,
                192 => bytes[1] == 168,
                _ => false
            },
            // Unique local, fc00::/7.
            AddressFamily.InterNetworkV6 => (bytes[0] & 0xFE) == 0xFC,
            _ => false
        };
    }

    private static bool IsPublicV4(IPAddress address)
    {
        var octets = address.GetAddressBytes();

        return octets[0] switch
        {
            // This network, and 0.0.0.0 ("here" on some stacks).
            0 => false,
            // Loopback.
            127 => false,
            // Private.
            10 => false,
            // Carrier-grade NAT (100.64/10) and link-local (169.254/16).
            100 => octets[1] is < 64 or > 127,
            169 => octets[1] != 254,
            172 => octets[1] is < 16 or > 31,
            192 => octets[1] switch
            {
                // Private, and the documentation range some resolvers return for nonexistent names.
                168 => false,
                0 => false,
                _ => true
            },
            198 => octets[1] is < 18 or > 19,
            // Documentation (203.0.113/24).
            203 => octets[1] != 0 || octets[2] != 113,
            // Multicast, broadcast and everything reserved above it.
            >= 224 => false,
            _ => true
        };
    }

    private static bool IsPublicV6(IPAddress address)
    {
        var bytes = address.GetAddressBytes();

        // Only global unicast (2000::/3) is the IPv6 internet; the rest (loopback, link-local, unique local,
        // multicast, NAT64, IPv4-compatible) is never a recipe website.
        if ((bytes[0] & 0xE0) != 0x20)
        {
            return false;
        }

        // Documentation, 2001:db8::/32.
        return !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8);
    }
}
