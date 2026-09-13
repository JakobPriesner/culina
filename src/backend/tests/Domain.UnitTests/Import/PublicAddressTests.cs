using System.Net;
using Domain.Import;

namespace Domain.UnitTests.Import;

/// <summary>
/// The one piece of this codebase where a mistake is a hole rather than a bug.
/// </summary>
/// <remarks>
/// "Paste a link and I will read it" means the server does the reading, from
/// inside whatever network it is deployed in. Every address below is one
/// somebody has actually used to make a server read something it should not
/// have, and each is here because leaving it out is how the next one gets in.
/// </remarks>
public class PublicAddressTests
{
    [Theory]
    // The cloud metadata service, which hands out credentials to anything that
    // asks from inside the instance. The reason this class exists.
    [InlineData("169.254.169.254")]
    [InlineData("169.254.0.1")]
    // Loopback, in all its spellings.
    [InlineData("127.0.0.1")]
    [InlineData("127.1.2.3")]
    [InlineData("0.0.0.0")]
    [InlineData("0.1.2.3")]
    // Private ranges: the network the container is sitting in.
    [InlineData("10.0.0.1")]
    [InlineData("10.255.255.255")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    // Carrier-grade NAT, benchmarking, documentation, multicast, broadcast.
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.255")]
    [InlineData("198.18.0.1")]
    [InlineData("192.0.2.1")]
    [InlineData("203.0.113.1")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    public void IsPublic_ShouldRefuse_EveryAddressThatIsNotTheOpenInternet(string address)
    {
        // Arrange & Act & Assert
        Assert.False(PublicAddress.IsPublic(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("::1")]
    [InlineData("::")]
    // Unique local: IPv6's private range.
    [InlineData("fc00::1")]
    [InlineData("fd12:3456::1")]
    // Link-local, and the NAT64 prefix, which is a door into whatever the
    // translator on the other side of it can reach.
    [InlineData("fe80::1")]
    [InlineData("64:ff9b::7f00:1")]
    [InlineData("ff02::1")]
    public void IsPublic_ShouldRefuse_TheSameRangesInIPv6(string address)
    {
        // Arrange & Act & Assert
        Assert.False(PublicAddress.IsPublic(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("::ffff:10.0.0.1")]
    public void IsPublic_ShouldRefuse_APrivateAddressWrappedInIPv6(string address)
    {
        // Arrange & Act & Assert
        // An IPv4 address written as IPv6 is the same address. Checking the
        // wrapper instead of the value is exactly how ::ffff:127.0.0.1 gets in.
        Assert.False(PublicAddress.IsPublic(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("1.1.1.1")]
    [InlineData("8.8.8.8")]
    [InlineData("93.184.216.34")]
    [InlineData("172.15.255.255")]
    [InlineData("172.32.0.1")]
    [InlineData("100.63.255.255")]
    [InlineData("100.128.0.1")]
    [InlineData("169.253.0.1")]
    [InlineData("192.169.0.1")]
    [InlineData("203.0.114.1")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("2001:4860:4860::8888")]
    public void IsPublic_ShouldAllow_AnOrdinaryWebsite(string address)
    {
        // Arrange & Act & Assert
        // The edges matter as much as the middles: 172.15 and 172.32 are both
        // public, and a range check written with the wrong comparison would
        // block half the internet or let the private network through.
        Assert.True(PublicAddress.IsPublic(IPAddress.Parse(address)));
    }

    [Fact]
    public void IsPublic_ShouldRefuse_Nothing()
    {
        // Arrange & Act & Assert
        Assert.False(PublicAddress.IsPublic(null));
    }
}
