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
    /// <summary>
    /// Cloud metadata services that live inside a private range rather than at
    /// the link-local address most clouds use: Alibaba's, and AWS's over IPv6.
    /// </summary>
    /// <remarks>
    /// Compared as bytes, not with <see cref="IPAddress.Equals(object)"/>,
    /// which also compares an IPv6 scope id — and <c>fd00:ec2::254%1</c> is the
    /// same machine as <c>fd00:ec2::254</c>.
    /// </remarks>
    private static readonly byte[][] CloudMetadata =
    [
        [100, 100, 100, 200],
        IPAddress.Parse("fd00:ec2::254").GetAddressBytes()
    ];

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

    /// <summary>
    /// Whether an address is on a private network an operator may opt into.
    /// </summary>
    /// <param name="address">A resolved address, never a host name.</param>
    /// <remarks>
    /// <para>
    /// The ranges a home or a container network actually hands out: RFC 1918,
    /// the shared space (100.64.0.0/10) that Tailscale and carrier NAT use, and
    /// IPv6 unique local addresses. That is where somebody's own recipe server
    /// is, and it is all that allowing private addresses allows.
    /// </para>
    /// <para>
    /// Never anything else, whatever the operator said. Loopback is this very
    /// server and its admin ports; link-local is where cloud metadata answers;
    /// 0.0.0.0 means "here" on some stacks; multicast and broadcast are not one
    /// machine at all. None of those is where a recipe library lives, and each
    /// is where an attack starts.
    /// </para>
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
        var bytes = address.GetAddressBytes();

        // Global unicast, 2000::/3, is the whole of the IPv6 internet. Outside
        // it are loopback, the unspecified address, link-local, unique local,
        // multicast, the NAT64 prefix — a door into whatever the translator
        // can reach — and the old ways of writing an IPv4 address in IPv6
        // (::127.0.0.1). None of them is a recipe website.
        if ((bytes[0] & 0xE0) != 0x20)
        {
            return false;
        }

        // Documentation, 2001:db8::/32.
        return !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8);
    }
}
