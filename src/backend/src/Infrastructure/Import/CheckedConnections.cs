using System.Net;
using System.Net.Sockets;
using Domain.Import;

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
    /// <summary>A connect callback for <see cref="SocketsHttpHandler"/>.</summary>
    /// <param name="allowPrivate">
    /// Whether an address on a private network may be reached. False for
    /// anything a stranger can aim — a pasted link — and true only for a
    /// connection the operator has opted into, because their own recipe server
    /// is very often the machine next door.
    /// </param>
    internal static Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>> To(
        bool allowPrivate) =>
        async (context, cancellationToken) =>
        {
            var host = context.DnsEndPoint.Host;

            var addresses = IPAddress.TryParse(host, out var literal)
                ? [literal]
                : await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);

            var allowed = Array.Find(addresses, address => allowPrivate || PublicAddress.IsPublic(address))
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
