using System.Net;
using System.Net.Sockets;

namespace Infrastructure.Import;

/// <summary>Opens sockets only to addresses that have been checked, shared by everything that fetches on a user's behalf.</summary>
/// <remarks>
/// It connects to the <em>address it resolved and checked</em>, not the host name, which closes the
/// DNS-rebinding window between validation and connect.
/// </remarks>
internal static class CheckedConnections
{
    /// <summary>A handler whose every connection goes to a checked address.</summary>
    /// <remarks>
    /// <paramref name="admits"/> chooses which resolved addresses may be reached (public for pasted links, wider for
    /// operator-approved connections); <paramref name="dialling"/> is told about each connection before it is dialled.
    /// Never through a proxy (it would check the proxy's address, and <c>HTTP_PROXY</c> would silently disable every
    /// check) and never following redirects by itself: an unchecked second request.
    /// </remarks>
    internal static SocketsHttpHandler Handler(
        Func<IPAddress, bool> admits,
        Action<DnsEndPoint, IPAddress>? dialling = null) => new()
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            ConnectCallback = To(admits, dialling)
        };

    private static Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>> To(
        Func<IPAddress, bool> admits,
        Action<DnsEndPoint, IPAddress>? dialling) =>
        async (context, cancellationToken) =>
        {
            var host = context.DnsEndPoint.Host;

            var addresses = IPAddress.TryParse(host, out var literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);

            var allowed = Array.Find(addresses, address => admits(address))
                ?? throw new InvalidOperationException("The address is not one this may connect to.");

            dialling?.Invoke(context.DnsEndPoint, allowed);

#pragma warning disable CA2000 // The stream returned below owns the socket.
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
#pragma warning restore CA2000

            try
            {
                await socket
                    .ConnectAsync(new IPEndPoint(allowed, context.DnsEndPoint.Port), cancellationToken)
                    .ConfigureAwait(false);

                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();

                throw;
            }
        };
}
