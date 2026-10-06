using System.Net;
using System.Net.Sockets;

namespace Infrastructure.Import;

/// <summary>
/// Opens sockets only to addresses that have been checked.
/// </summary>
/// <remarks>
/// <para>
/// Shared by everything that makes the server fetch on a user's behalf, and the
/// reason it is shared is that getting it slightly different in two places is
/// how one of them ends up wrong.
/// </para>
/// <para>
/// The load-bearing part is that it connects to the <em>address it resolved and
/// checked</em>, not to the host name. Validating a name and then handing the
/// name to a socket leaves a window in which the name can resolve to something
/// else, and that window is DNS rebinding. Handing back a socket already
/// connected to a checked address closes it.
/// </para>
/// </remarks>
internal static class CheckedConnections
{
    /// <summary>
    /// A handler whose every connection goes to a checked address.
    /// </summary>
    /// <param name="admits">
    /// Which resolved addresses may be reached. Public only for anything a
    /// stranger can aim — a pasted link — and wider only for a connection the
    /// operator has opted into, because their own recipe server is very often
    /// the machine next door.
    /// </param>
    /// <remarks>
    /// <para>
    /// Never through a proxy. Through one, the connection this handler opens is
    /// to the proxy, so the address that gets checked is the proxy's — and the
    /// proxy then connects wherever it is asked, cloud metadata and the
    /// database container included. Left at its default, an
    /// <c>HTTP_PROXY</c> or <c>HTTPS_PROXY</c> in the environment would switch
    /// every check here off without anything saying so.
    /// </para>
    /// <para>
    /// Never following a redirect by itself, either: a followed redirect is a
    /// second request nobody checked the address of. Whoever wants redirects
    /// follows them by hand, through this handler again.
    /// </para>
    /// </remarks>
    internal static SocketsHttpHandler Handler(Func<IPAddress, bool> admits) => new()
    {
        UseProxy = false,
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.All,
        ConnectTimeout = TimeSpan.FromSeconds(5),
        ConnectCallback = To(admits)
    };

    private static Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>> To(
        Func<IPAddress, bool> admits) =>
        async (context, cancellationToken) =>
        {
            var host = context.DnsEndPoint.Host;

            var addresses = IPAddress.TryParse(host, out var literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);

            var allowed = Array.Find(addresses, address => admits(address))
                ?? throw new InvalidOperationException("The address is not one this may connect to.");

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
