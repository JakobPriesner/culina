using System.Net;
using System.Net.Sockets;
using Application.Abstractions;
using Domain.Import;
using Infrastructure.Import;
using Microsoft.Extensions.Logging.Abstractions;
using TestSupport;

namespace IntegrationTests.Import;

/// <summary>
/// The fetcher actually refusing to fetch.
/// </summary>
/// <remarks>
/// <see cref="Domain.UnitTests"/> proves the address rules; this proves they
/// are wired to a socket. A guard that is correct and unreachable is not a
/// guard, and "the server will not read its own network" is the kind of claim
/// that has to be demonstrated rather than reviewed.
///
/// Nothing here reaches the internet. The one server it talks to is one these
/// tests start, on loopback — which is itself the address that must be refused.
/// </remarks>
public class SafeWebPageFetcherTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SafeWebPageFetcher NewFetcher() =>
        new(NullLogger<SafeWebPageFetcher>.Instance);

    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://localhost/")]
    [InlineData("http://[::1]/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://10.0.0.1/")]
    [InlineData("http://192.168.0.1/")]
    [InlineData("http://[::ffff:127.0.0.1]/")]
    public async Task FetchAsync_ShouldRefuse_AnAddressInsideTheNetwork(string url)
    {
        // Arrange
        using var fetcher = NewFetcher();

        // Act
        var result = await fetcher.FetchAsync(new Uri(url), Token).ConfigureAwait(true);

        // Assert
        // Refused, and refused vaguely: telling a caller which address was
        // blocked and which merely timed out is a port scanner with a friendly
        // error message.
        result.ShouldBeFailure(ImportErrors.CouldNotFetch);
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("gopher://example.com/")]
    [InlineData("ftp://example.com/")]
    public async Task FetchAsync_ShouldRefuse_ASchemeThatIsNotTheWeb(string url)
    {
        // Arrange
        using var fetcher = NewFetcher();

        // Act
        var result = await fetcher.FetchAsync(new Uri(url), Token).ConfigureAwait(true);

        // Assert
        // `file:` reads the disk and `gopher:` writes arbitrary bytes to an
        // arbitrary port. Neither has ever held a recipe.
        result.ShouldBeFailure(ImportErrors.UnreachableAddress);
    }

    [Fact]
    public async Task FetchAsync_ShouldRefuse_AServerItCanActuallyReach()
    {
        // Arrange
        // A real listener, on loopback, answering a real page. Everything about
        // this request works except the address — which is the whole point.
        using var server = new LoopbackServer("<html><body><p>Secret</p></body></html>");
        using var fetcher = NewFetcher();

        // Act
        var result = await fetcher.FetchAsync(server.Url, Token).ConfigureAwait(true);

        // Assert
        result.ShouldBeFailure(ImportErrors.CouldNotFetch);
        Assert.Equal(0, server.Requests);
    }

    [Fact]
    public async Task FetchAsync_ShouldRefuse_ARedirectIntoTheNetwork()
    {
        // Arrange
        // The hop that an automatic redirect would take without any of the
        // checks: a public-looking page that points inwards.
        using var server = new LoopbackServer("<html></html>");
        using var fetcher = NewFetcher();

        // Act
        var result = await fetcher
            .FetchAsync(new Uri($"http://127.0.0.1:{server.Port}/"), Token)
            .ConfigureAwait(true);

        // Assert
        result.ShouldBeFailure(ImportErrors.CouldNotFetch);
    }
}

/// <summary>A real HTTP server on loopback, which is the address to refuse.</summary>
internal sealed class LoopbackServer : IDisposable
{
    private readonly TcpListener listener;
    private int requests;

    internal LoopbackServer(string body, int status = 200, string contentType = "text/html")
    {
        listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;

        _ = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    using var client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);

                    Interlocked.Increment(ref requests);

                    var response =
                        $"HTTP/1.1 {status} Answered\r\nContent-Type: {contentType}\r\n"
                        + $"Content-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}";

                    var stream = client.GetStream();

                    // The request is read before answering: closing a socket
                    // with unread bytes in it resets the connection, and the
                    // client would see that instead of the answer.
                    await ReadRequestAsync(stream).ConfigureAwait(false);

                    await stream
                        .WriteAsync(System.Text.Encoding.ASCII.GetBytes(response))
                        .ConfigureAwait(false);
                }
            }
            catch (SocketException)
            {
                // The listener was stopped, which is how this ends.
            }
            catch (IOException)
            {
                // A client gave up mid-request, which ends a test server too.
            }
            catch (ObjectDisposedException)
            {
                // The same, seen from the other side.
            }
        });
    }

    internal int Port { get; }

    internal Uri Url => new($"http://127.0.0.1:{Port}/");

    /// <summary>How many connections it actually received. Must stay zero.</summary>
    internal int Requests => Volatile.Read(ref requests);

    private static async Task ReadRequestAsync(Stream stream)
    {
        var seen = new List<byte>();
        var buffer = new byte[1024];

        while (!EndsWithBlankLine(seen))
        {
            var read = await stream.ReadAsync(buffer).ConfigureAwait(false);

            if (read == 0)
            {
                return;
            }

            seen.AddRange(buffer.AsSpan(0, read));
        }
    }

    private static bool EndsWithBlankLine(List<byte> seen) =>
        seen.Count >= 4 && seen[^4] == '\r' && seen[^3] == '\n' && seen[^2] == '\r' && seen[^1] == '\n';

    public void Dispose()
    {
        listener.Stop();
        listener.Dispose();
    }
}
