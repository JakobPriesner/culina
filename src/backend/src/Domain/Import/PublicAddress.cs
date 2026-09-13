using System.Net;
using System.Net.Sockets;

namespace Domain.Import;

/// <summary>
/// Whether an address is one the server may be asked to fetch.
/// </summary>
/// <remarks>
/// <para>
/// The whole of the danger in "paste a link and I will read it" is that the
/// server does the reading. Without this, anyone with an account could aim it
/// at <c>169.254.169.254</c> and read a cloud instance's credentials, or walk
/// the private network the container sits in and use the timing of the replies
/// as a port scanner.
/// </para>
/// <para>
/// A deny list of ranges rather than an allow list of hosts, because the
/// feature is "any recipe website". What it denies is everything that is not
/// the public internet — and it is applied to the <em>resolved address</em>,
/// every time, including after each redirect, because a name that resolved
/// publicly a moment ago can resolve to 127.0.0.1 on the next lookup.
/// </para>
/// </remarks>
public static class PublicAddress
{
    /// <summary>Whether the server may open a connection to this address.</summary>
    /// <param name="address">A resolved address, never a host name.</param>
    public static bool IsPublic(IPAddress? address)
    {
        if (address is null)
        {
            return false;
        }

        // An IPv4 address written as IPv6 is the same address, and checking the
        // wrapper instead of the value is how ::ffff:127.0.0.1 gets through.
        if (address.IsIPv4MappedToIPv6)
        {
            return IsPublic(address.MapToIPv4());
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPublicV4(address),
            AddressFamily.InterNetworkV6 => IsPublicV6(address),
            // Anything else is a unix socket or something stranger, and no
            // recipe website has ever been at one.
            _ => false
        };
    }

    private static bool IsPublicV4(IPAddress address)
    {
        var octets = address.GetAddressBytes();

        return octets[0] switch
        {
            // This network, and 0.0.0.0 — which on some stacks means "here".
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
                // Private, and the documentation range that some resolvers
                // hand back for a name that does not exist.
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
        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
        {
            return false;
        }

        if (address.Equals(IPAddress.IPv6Loopback) || address.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();

        // Unique local (fc00::/7), and the NAT64 well-known prefix, which is a
        // door into whatever the translator can reach.
        return (bytes[0] & 0xFE) != 0xFC
            && !(bytes[0] == 0x00 && bytes[1] == 0x64 && bytes[2] == 0xFF && bytes[3] == 0x9B);
    }
}
