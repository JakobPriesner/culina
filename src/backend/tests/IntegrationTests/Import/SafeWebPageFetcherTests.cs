using System.Net;
using System.Net.Sockets;
using Application.Abstractions;
using Domain.Import;
using Infrastructure.Import;
using Microsoft.Extensions.Logging.Abstractions;
using TestSupport;

namespace IntegrationTests.Import;

/// <summary>
/// The fetcher actually refusing to fetch: proves the address rules are wired to a socket. Nothing reaches the internet;
/// the one server is started here on loopback, the very address that must be refused.
/// </summary>
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
        using var fetcher = NewFetcher();

        var result = await fetcher.FetchAsync(new Uri(url), Token).ConfigureAwait(true);

        // Refused vaguely: saying which address was blocked and which timed out is a port scanner.
        result.ShouldBeFailure(ImportErrors.CouldNotFetch);
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("gopher://example.com/")]
    [InlineData("ftp://example.com/")]
    public async Task FetchAsync_ShouldRefuse_ASchemeThatIsNotTheWeb(string url)
    {
        using var fetcher = NewFetcher();

        var result = await fetcher.FetchAsync(new Uri(url), Token).ConfigureAwait(true);

        // `file:` reads the disk and `gopher:` writes arbitrary bytes to an arbitrary port.
        result.ShouldBeFailure(ImportErrors.UnreachableAddress);
    }

    [Fact]
    public async Task FetchAsync_ShouldRefuse_AServerItCanActuallyReach()
    {
        // A real loopback listener answering a real page: everything works except the address.
        using var server = new LoopbackServer("<html><body><p>Secret</p></body></html>");
        using var fetcher = NewFetcher();

        var result = await fetcher.FetchAsync(server.Url, Token).ConfigureAwait(true);

        result.ShouldBeFailure(ImportErrors.CouldNotFetch);
        Assert.Equal(0, server.Requests);
    }

    [Fact]
    public async Task FetchAsync_ShouldRefuse_ARedirectIntoTheNetwork()
    {
        // The hop an automatic redirect would take unchecked: a public-looking page pointing inwards.
        using var server = new LoopbackServer("<html></html>");
        using var fetcher = NewFetcher();

        var result = await fetcher
            .FetchAsync(new Uri($"http://127.0.0.1:{server.Port}/"), Token)
            .ConfigureAwait(true);

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

                    // Read before answering: closing a socket with unread bytes resets the connection.
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
