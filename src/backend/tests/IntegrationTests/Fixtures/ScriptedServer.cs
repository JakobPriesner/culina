using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace IntegrationTests.Fixtures;

/// <summary>
/// Something listening on a loopback port that is not PostgreSQL: it reads the client's startup
/// message, answers with fixed bytes and keeps what the client sends after that.
/// </summary>
public sealed class ScriptedServer : IDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly Task<byte[]> conversation;

    public ScriptedServer(byte[] reply)
    {
        listener.Start();
        conversation = ServeAsync(reply);
    }

    /// <summary>The port it listens on, on 127.0.0.1.</summary>
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

    /// <summary>Everything the client sent after the reply, once it hung up.</summary>
    public Task<byte[]> ReceivedAfterReply => conversation;

    /// <summary>A loopback port nothing listens on.</summary>
    public static int ClosedPort()
    {
        // Stopped again on the way out, so the port is free and refuses.
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();

        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }

    public void Dispose() => listener.Dispose();

    private async Task<byte[]> ServeAsync(byte[] reply)
    {
        using var client = await listener.AcceptTcpClientAsync();
        var stream = client.GetStream();

        // The startup message: its length, itself included, then the rest.
        var length = new byte[4];
        await stream.ReadExactlyAsync(length);
        await stream.ReadExactlyAsync(new byte[BinaryPrimitives.ReadInt32BigEndian(length) - 4]);

        // And nothing more: the client sees the end of the stream once it has
        // read the reply, rather than waiting out its timeout for the rest.
        await stream.WriteAsync(reply);
        client.Client.Shutdown(SocketShutdown.Send);

        using var after = new MemoryStream();
        using var patience = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            await stream.CopyToAsync(after, patience.Token);
        }
        catch (Exception hungUp) when (hungUp is IOException or OperationCanceledException)
        {
            // The client closed the connection, which is the expected ending.
        }

        return after.ToArray();
    }
}
