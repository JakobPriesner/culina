using System.Net;
using Domain.Import;
using Infrastructure.Import;

namespace IntegrationTests.Import;

/// <summary>
/// The checked handler with a proxy configured, which it must ignore.
/// </summary>
/// <remarks>
/// <para>
/// A proxy is what <c>HTTP_PROXY</c> or <c>HTTPS_PROXY</c> in the environment
/// becomes: <see cref="HttpClient.DefaultProxy"/> reads them, and a handler
/// left at <c>UseProxy = true</c> uses that proxy unless it was given one of
/// its own. These tests give it one of its own rather than setting the
/// variables, because the default proxy is read once per process and is shared
/// by every test running beside these.
/// </para>
/// <para>
/// Both servers are on loopback. One plays the target, the other the proxy,
/// and each counts the connections it was sent.
/// </para>
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
        // A rule that admits loopback, so the only thing that can decide where
        // the connection goes is whether the proxy is used. Through the proxy,
        // the check would be made against the proxy's address and the target's
        // would never be looked at.
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
