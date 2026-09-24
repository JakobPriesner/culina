using System.Net;
using Application.Settings.GetServer;
using Microsoft.AspNetCore.HttpOverrides;

namespace Api.Endpoints.Settings.GetServer.V1;

/// <summary>Reads, from the request itself, how it reached the server.</summary>
internal static class GetServerSettingsRequestExtensions
{
    /// <remarks>
    /// Read after the forwarded-headers middleware has had its say. When it
    /// trusted the proxy it replaced the address and left the proxy's in
    /// <c>X-Original-For</c>; when it did not, the address is still the
    /// proxy's and <c>X-Forwarded-For</c> is still there.
    /// </remarks>
    internal static GetServerSettingsQuery ToQuery(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var headers = context.Request.Headers;
        var trusted = headers.ContainsKey(ForwardedHeadersDefaults.XOriginalForHeaderName);

        return new GetServerSettingsQuery(
            Readable(context.Connection.RemoteIpAddress),
            trusted || headers.ContainsKey(ForwardedHeadersDefaults.XForwardedForHeaderName),
            trusted);
    }

    /// <summary>
    /// A dual-stack listener reports an IPv4 client as <c>::ffff:172.18.0.5</c>.
    /// The proxy list compares it as IPv4 too, so that is the form to offer.
    /// </summary>
    private static string? Readable(IPAddress? address) =>
        address is null ? null : (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
}
