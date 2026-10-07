using System.Net;
using Domain.Import;
using Infrastructure.Import;

namespace IntegrationTests.Import;

/// <summary>The checked handler with a proxy configured, which it must ignore.</summary>
/// <remarks>
/// The tests set a handler proxy instead of <c>HTTP_PROXY</c>, as <see cref="HttpClient.DefaultProxy"/> is
/// read once per process and shared with other tests.
/// </remarks>
public class CheckedConnectionsTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Handler_ShouldRefuseALoopbackTarget_WithoutContactingTheProxy_WhenOneIsConfigured()
    {
        // Arrange
        using var proxy = new LoopbackServer("proxied");
        using var target = new LoopbackServer("<html></html>");
        using var handler = CheckedConnections.Handler(admits: PublicAddress.IsPublic);
        handler.Proxy = new WebProxy(proxy.Url);
        using var client = new HttpClient(handler);

        // Act
        var failure = await Record.ExceptionAsync(() => client.GetAsync(target.Url, Token));

        // Assert
        Assert.IsType<HttpRequestException>(failure);
        Assert.Equal(0, target.Requests);
        Assert.Equal(0, proxy.Requests);
    }

    [Fact]
    public async Task Handler_ShouldConnectToTheTargetItself_WhenAProxyIsConfigured()
    {
        // Arrange
        // Admits loopback, so only proxy use decides where the connection goes; through a proxy the
        // check would see the proxy's address, never the target's.
        using var proxy = new LoopbackServer("proxied");
        using var target = new LoopbackServer("<html></html>");
        using var handler = CheckedConnections.Handler(admits: _ => true);
        handler.Proxy = new WebProxy(proxy.Url);
        using var client = new HttpClient(handler);

        // Act
        using var response = await client.GetAsync(target.Url, Token);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, target.Requests);
        Assert.Equal(0, proxy.Requests);
    }
}
