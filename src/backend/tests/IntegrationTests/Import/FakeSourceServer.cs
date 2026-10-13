using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace IntegrationTests.Import;

/// <summary>A real HTTP server on loopback that answers as somebody's recipe app would, and remembers what it was asked.</summary>
/// <remarks>
/// A real socket, not a stubbed handler: what is proved includes the client opening several connections
/// at once, which a fake at the wrong layer would answer by construction.
/// </remarks>
internal sealed class FakeSourceServer : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly CancellationTokenSource stopping = new();
    private readonly ConcurrentQueue<SeenRequest> seen = new();
    private readonly Func<HttpListenerContext, string, Task> answer;

    private FakeSourceServer(Func<HttpListenerContext, string, Task> answer)
    {
        this.answer = answer;
        Address = $"http://127.0.0.1:{FreePort().ToString(CultureInfo.InvariantCulture)}";
    }

    public string Address { get; }

    /// <summary>Every request it received, in the order they arrived.</summary>
    public IReadOnlyList<SeenRequest> Requests => [.. seen];

    /// <summary>Starts answering with <paramref name="answer"/>, which is handed the request and the body it carried.</summary>
    public static FakeSourceServer Start(Func<HttpListenerContext, string, Task> answer)
    {
        var server = new FakeSourceServer(answer);

        server.listener.Prefixes.Add($"{server.Address}/");
        server.listener.Start();

        _ = Task.Run(server.ServeAsync);

        return server;
    }

    public static async Task WriteAsync(HttpListenerContext context, int status, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);

        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;

        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);

        context.Response.Close();
    }

    public void Dispose()
    {
        stopping.Cancel();
        listener.Close();
        stopping.Dispose();
    }

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);

        probe.Start();

        var port = ((IPEndPoint)probe.LocalEndpoint).Port;

        probe.Stop();

        return port;
    }

    private async Task ServeAsync()
    {
        while (!stopping.IsCancellationRequested)
        {
            HttpListenerContext context;

            try
            {
                context = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            _ = Task.Run(() => AnswerAsync(context));
        }
    }

    private async Task AnswerAsync(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
        var body = await reader.ReadToEndAsync().ConfigureAwait(false);

        seen.Enqueue(new SeenRequest(
            context.Request.HttpMethod,
            context.Request.RawUrl ?? string.Empty,
            context.Request.Headers["Authorization"],
            body));

        await answer(context, body).ConfigureAwait(false);
    }
}

/// <summary>One request as the fake received it.</summary>
internal sealed record SeenRequest(string Method, string PathAndQuery, string? Authorization, string Body)
{
    /// <summary>The path without its query.</summary>
    public string Path => PathAndQuery.Split('?')[0];
}
